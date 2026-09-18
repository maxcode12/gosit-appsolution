using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Messaging;

namespace GoSit.Application.Features.Messaging.Queries;

public sealed record ListConversationsQuery : IQuery<IReadOnlyList<ConversationSummary>>;

/// <summary>
/// One thread's messages, newest page first. Reading also marks the thread read for the
/// caller, which is what opening an inbox means.
/// </summary>
public sealed record GetConversationQuery(Guid ConversationId, int Take = 50, DateTime? Before = null)
    : IQuery<ConversationDetail>;

public sealed record MarkConversationReadCommand(Guid ConversationId)
    : ICommand<CareRecipients.RemoveCareRecipient.Unit>;

/// <summary>
/// Resolves which profile the caller is, on either side of the marketplace. Shared so
/// participation is decided the same way for every messaging operation.
/// </summary>
public sealed class MessagingParticipantResolver(
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles)
{
    public sealed record Participant(Guid ProfileId, bool IsCaregiver);

    public async Task<Participant?> ResolveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var caregiver = await caregiverProfiles.FindByUserIdAsync(userId, cancellationToken);

        if (caregiver is not null)
        {
            return new Participant(caregiver.Id, IsCaregiver: true);
        }

        var requester = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken);

        return requester is null ? null : new Participant(requester.Id, IsCaregiver: false);
    }
}

public sealed class ListConversationsHandler(
    IConversationRepository conversations,
    MessagingParticipantResolver participants,
    IProfileDirectory profiles,
    ICurrentUser currentUser) : IQueryHandler<ListConversationsQuery, IReadOnlyList<ConversationSummary>>
{
    /// <summary>Enough of the last message to recognise the thread, not to read it.</summary>
    private const int PreviewLength = 120;

    public async Task<IReadOnlyList<ConversationSummary>> HandleAsync(
        ListConversationsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var participant = await participants.ResolveAsync(userId, cancellationToken);

        // No profile means no conversations — an empty inbox, not an error.
        if (participant is null)
        {
            return [];
        }

        var threads = await conversations.ListForParticipantAsync(
            participant.ProfileId, participant.IsCaregiver, cancellationToken);

        if (threads.Count == 0)
        {
            return [];
        }

        var otherIds = threads
            .Select(c => Other(c, participant))
            .Distinct()
            .ToList();

        var names = await profiles.ListProfileNamesAsync(otherIds, cancellationToken);

        return threads
            .Select(conversation =>
            {
                var otherId = Other(conversation, participant);
                var name = names.GetValueOrDefault(otherId);
                var last = conversation.Messages.MaxBy(m => m.SentAtUtc);

                return new ConversationSummary(
                    conversation.Id,
                    otherId,
                    name?.FirstName ?? "GoSit user",
                    name is { LastName.Length: > 0 } ? name.LastName[..1] : string.Empty,
                    last is null ? null : Preview(last.Body),
                    conversation.LastMessageAtUtc,
                    conversation.UnreadCountFor(participant.ProfileId));
            })
            .ToList();
    }

    private static Guid Other(Conversation conversation, MessagingParticipantResolver.Participant participant) =>
        participant.IsCaregiver ? conversation.CareRequesterProfileId : conversation.CaregiverProfileId;

    private static string Preview(string body) =>
        body.Length <= PreviewLength ? body : body[..PreviewLength] + "…";
}

public sealed class GetConversationHandler(
    IConversationRepository conversations,
    MessagingParticipantResolver participants,
    IProfileDirectory profiles,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IQueryHandler<GetConversationQuery, ConversationDetail>
{
    private const int MaxTake = 100;

    public async Task<ConversationDetail> HandleAsync(
        GetConversationQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var conversation = await conversations.FindByIdAsync(query.ConversationId, cancellationToken)
            ?? throw new NotFoundException("That conversation does not exist.");

        var participant = await participants.ResolveAsync(userId, cancellationToken);

        // Someone who is not in this thread is told it does not exist, so conversation
        // ids cannot be probed and private messages cannot leak by id guessing.
        if (participant is null || !conversation.IsParticipant(participant.ProfileId))
        {
            throw new NotFoundException("That conversation does not exist.");
        }

        var take = Math.Clamp(query.Take, 1, MaxTake);

        var ordered = conversation.Messages
            .Where(m => query.Before is null || m.SentAtUtc < query.Before)
            .OrderByDescending(m => m.SentAtUtc)
            .ToList();

        var page = ordered.Take(take).OrderBy(m => m.SentAtUtc).ToList();

        // Opening a thread is reading it.
        conversation.MarkRead(participant.ProfileId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var otherId = participant.IsCaregiver
            ? conversation.CareRequesterProfileId
            : conversation.CaregiverProfileId;

        var names = await profiles.ListProfileNamesAsync([otherId], cancellationToken);
        var name = names.GetValueOrDefault(otherId);

        return new ConversationDetail(
            conversation.Id,
            otherId,
            name?.FirstName ?? "GoSit user",
            name is { LastName.Length: > 0 } ? name.LastName[..1] : string.Empty,
            page
                .Select(m => new MessageResponse(
                    m.Id,
                    m.SenderProfileId,
                    m.SenderProfileId == participant.ProfileId,
                    m.Body,
                    m.SentAtUtc))
                .ToList(),
            HasMore: ordered.Count > take);
    }
}

public sealed class MarkConversationReadHandler(
    IConversationRepository conversations,
    MessagingParticipantResolver participants,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<MarkConversationReadCommand, CareRecipients.RemoveCareRecipient.Unit>
{
    public async Task<CareRecipients.RemoveCareRecipient.Unit> HandleAsync(
        MarkConversationReadCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var conversation = await conversations.FindByIdAsync(command.ConversationId, cancellationToken)
            ?? throw new NotFoundException("That conversation does not exist.");

        var participant = await participants.ResolveAsync(userId, cancellationToken);

        if (participant is null || !conversation.IsParticipant(participant.ProfileId))
        {
            throw new NotFoundException("That conversation does not exist.");
        }

        conversation.MarkRead(participant.ProfileId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CareRecipients.RemoveCareRecipient.Unit.Value;
    }
}
