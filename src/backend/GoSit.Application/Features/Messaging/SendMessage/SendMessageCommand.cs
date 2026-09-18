using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Messaging;
using GoSit.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Messaging.SendMessage;

/// <summary>
/// Opens a conversation with a caregiver, or adds to the existing one.
///
/// A care requester may start a thread with any listable caregiver. A caregiver cannot
/// start one — they reply through <see cref="ReplyToConversationCommand"/> — so the
/// marketplace never becomes a channel for unsolicited approaches to families.
/// </summary>
public sealed record SendMessageCommand(Guid CaregiverProfileId, string Body)
    : ICommand<MessageResponse>;

/// <summary>Adds to an existing thread, from either side.</summary>
public sealed record ReplyToConversationCommand(Guid ConversationId, string Body)
    : ICommand<MessageResponse>;

public sealed class SendMessageValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.CaregiverProfileId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(Message.MaxBodyLength);
    }
}

public sealed class ReplyToConversationValidator : AbstractValidator<ReplyToConversationCommand>
{
    public ReplyToConversationValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(Message.MaxBodyLength);
    }
}

/// <summary>
/// Persists the message, raises an in-app notification for the recipient, and pushes a
/// real-time update. Shared by both send paths so delivery behaves identically.
/// </summary>
public sealed class MessageDelivery(
    INotificationRepository notifications,
    IProfileDirectory profiles,
    IRealtimeNotifier realtime,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<MessageDelivery> logger)
{
    public async Task<MessageResponse> DeliverAsync(
        Conversation conversation,
        Message message,
        Guid senderProfileId,
        CancellationToken cancellationToken)
    {
        var utcNow = clock.UtcNow;
        var recipientProfileId = conversation.RecipientOf(senderProfileId);

        var senderIsCaregiver = senderProfileId == conversation.CaregiverProfileId;

        var recipientUserId = senderIsCaregiver
            ? await profiles.FindUserIdForCareRequesterAsync(recipientProfileId, cancellationToken)
            : await profiles.FindUserIdForCaregiverAsync(recipientProfileId, cancellationToken);

        var names = await profiles.ListProfileNamesAsync([senderProfileId], cancellationToken);

        if (recipientUserId is { } userId)
        {
            // A summary only. The body must never reach a notification, which is
            // delivered by email and push and lands on lock screens.
            notifications.Add(Notification.Create(
                userId,
                NotificationType.MessageReceived,
                $"New message from {DisplayName(names, senderProfileId)}",
                "Open GoSit to read it.",
                nameof(Conversation),
                conversation.Id,
                utcNow));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (recipientUserId is { } notifyUserId)
        {
            // Best effort: a dropped real-time push must not fail an already-saved message.
            try
            {
                await realtime.NotifyMessageReceivedAsync(
                    notifyUserId, conversation.Id, message.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex, "Could not push a real-time update for message {MessageId}", message.Id);
            }
        }

        if (message.FlaggedForReview)
        {
            // Identifiers only — the body is exactly what must not be logged.
            logger.LogInformation(
                "Message {MessageId} in conversation {ConversationId} was flagged for review",
                message.Id, conversation.Id);
        }

        return new MessageResponse(
            message.Id, message.SenderProfileId, SentByMe: true, message.Body, message.SentAtUtc);
    }

    private static string DisplayName(
        IReadOnlyDictionary<Guid, CaregiverName> names,
        Guid profileId)
    {
        if (!names.TryGetValue(profileId, out var name))
        {
            return "Someone";
        }

        var initial = name.LastName.Length > 0 ? $" {name.LastName[..1]}." : string.Empty;
        return $"{name.FirstName}{initial}";
    }
}

public sealed class SendMessageHandler(
    IConversationRepository conversations,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverSearchRepository caregivers,
    MessageDelivery delivery,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<SendMessageCommand, MessageResponse>
{
    public async Task<MessageResponse> HandleAsync(
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var requester = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your care-requester profile first.");

        // Only a listable caregiver can be contacted, so this cannot be used to probe
        // whether an arbitrary profile id exists.
        _ = await caregivers.FindListableByIdAsync(command.CaregiverProfileId, cancellationToken)
            ?? throw new NotFoundException("That caregiver is not available.");

        var utcNow = clock.UtcNow;

        var conversation = await conversations.FindBetweenAsync(
            requester.Id, command.CaregiverProfileId, cancellationToken);

        if (conversation is null)
        {
            conversation = Conversation.Start(requester.Id, command.CaregiverProfileId, null, utcNow);
            conversations.Add(conversation);
        }

        var message = conversation.SendMessage(requester.Id, command.Body, utcNow);

        return await delivery.DeliverAsync(conversation, message, requester.Id, cancellationToken);
    }
}

public sealed class ReplyToConversationHandler(
    IConversationRepository conversations,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    MessageDelivery delivery,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<ReplyToConversationCommand, MessageResponse>
{
    public async Task<MessageResponse> HandleAsync(
        ReplyToConversationCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var conversation = await conversations.FindByIdAsync(command.ConversationId, cancellationToken)
            ?? throw new NotFoundException("That conversation does not exist.");

        var senderProfileId = await ResolveParticipantAsync(userId, conversation, cancellationToken);

        var message = conversation.SendMessage(senderProfileId, command.Body, clock.UtcNow);

        return await delivery.DeliverAsync(conversation, message, senderProfileId, cancellationToken);
    }

    /// <summary>
    /// Which side of the conversation the caller is on. Someone who is on neither gets
    /// "not found" rather than "forbidden", so conversation ids cannot be probed.
    /// </summary>
    private async Task<Guid> ResolveParticipantAsync(
        Guid userId,
        Conversation conversation,
        CancellationToken cancellationToken)
    {
        var caregiver = await caregiverProfiles.FindByUserIdAsync(userId, cancellationToken);

        if (caregiver is not null && conversation.IsParticipant(caregiver.Id))
        {
            return caregiver.Id;
        }

        var requester = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken);

        if (requester is not null && conversation.IsParticipant(requester.Id))
        {
            return requester.Id;
        }

        throw new NotFoundException("That conversation does not exist.");
    }
}
