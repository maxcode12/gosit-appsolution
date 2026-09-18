using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Messaging;
using GoSit.Domain.TrustAndSafety;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Administration.Moderation;

/// <summary>
/// Staff review of messages flagged as possible contact sharing.
///
/// `ContactDetailPolicy` has flagged messages since Phase 5, the schema carries a partial
/// index built "for the moderation queue in Phase 7", and nothing has ever read the flag
/// but a log line. The policy's own comment reasons that it can afford to be loose because
/// "a false positive costs a review" — there was no review, so a false positive cost
/// nothing and a true positive cost nothing either.
///
/// Everything here is shaped by one fact: this is the one staff surface that reads private
/// messages. Taking care off-platform is a real safety problem — it removes the audit
/// trail, the verified identity and the payment protection — and that justifies someone
/// looking. It does not justify browsing.
/// </summary>
public sealed record ListFlaggedMessagesQuery(int Take = 50)
    : IQuery<IReadOnlyList<FlaggedMessageRow>>;

/// <summary>
/// Reads one flagged message. Separate from the queue, and audited, because this is the
/// request that discloses what somebody wrote.
/// </summary>
public sealed record ReadFlaggedMessageQuery(Guid MessageId) : IQuery<FlaggedMessageDetail>;

public sealed record FlaggedMessageDetail(
    Guid MessageId,
    Guid ConversationId,
    string SenderName,
    string RecipientName,
    bool SentByCaregiver,
    DateTime SentAtUtc,

    /// <summary>The message. The reason this endpoint is audited.</summary>
    string Body);

/// <summary>
/// Settles a flag: nothing wrong, or escalated to an incident about the sender.
///
/// <paramref name="Notes"/> is required for an escalation and becomes the incident's
/// description; it is the staff member's account of why a message warranted action.
/// </summary>
public sealed record ModerateMessageCommand(
    Guid MessageId,
    MessageModerationOutcome Outcome,
    string? Notes) : ICommand<Unit>;

public sealed class ModerateMessageValidator : AbstractValidator<ModerateMessageCommand>
{
    public ModerateMessageValidator()
    {
        RuleFor(x => x.Outcome).IsInEnum();

        // An escalation ends up as a report about a real person, and "escalated" with no
        // reason is not something the next staff member can act on.
        RuleFor(x => x.Notes)
            .NotEmpty()
            .MaximumLength(2000)
            .When(x => x.Outcome == MessageModerationOutcome.Escalated);
    }
}

public sealed class ListFlaggedMessagesHandler(IMessageModerationRepository moderation)
    : IQueryHandler<ListFlaggedMessagesQuery, IReadOnlyList<FlaggedMessageRow>>
{
    public Task<IReadOnlyList<FlaggedMessageRow>> HandleAsync(
        ListFlaggedMessagesQuery query,
        CancellationToken cancellationToken) =>
        moderation.ListAwaitingModerationAsync(Math.Clamp(query.Take, 1, 200), cancellationToken);
}

public sealed class ReadFlaggedMessageHandler(
    IMessageModerationRepository moderation,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    ILogger<ReadFlaggedMessageHandler> logger)
    : IQueryHandler<ReadFlaggedMessageQuery, FlaggedMessageDetail>
{
    public async Task<FlaggedMessageDetail> HandleAsync(
        ReadFlaggedMessageQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var message = await moderation.FindForReviewAsync(query.MessageId, cancellationToken)
            ?? throw new NotFoundException("That message is not flagged for review.");

        var parties = await moderation.FindPartiesAsync(query.MessageId, cancellationToken)
            ?? throw new NotFoundException("That message is not flagged for review.");

        /*
         * The safeguard this whole design rests on.
         *
         * A staff member reading a private message is an act, not a lookup, and the record
         * of who read whose message is what makes the queue accountable rather than a
         * window. It is written on a read, which is unusual and deliberate — the audit is
         * the price of the disclosure, so it commits before the body is returned.
         */
        audit.Record(nameof(Message), message.Id, "FlaggedMessageRead", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Identifiers only. Logging the body here would defeat the entire feature.
        logger.LogInformation(
            "Flagged message {MessageId} was read by {UserId}", message.Id, userId);

        return new FlaggedMessageDetail(
            message.Id,
            message.ConversationId,
            parties.SenderName,
            parties.RecipientName,
            message.SentByCaregiver,
            message.SentAtUtc,
            message.Body);
    }
}

public sealed class ModerateMessageHandler(
    IMessageModerationRepository moderation,
    IIncidentRepository incidents,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<ModerateMessageHandler> logger) : ICommandHandler<ModerateMessageCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        ModerateMessageCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var message = await moderation.FindForReviewAsync(command.MessageId, cancellationToken)
            ?? throw new NotFoundException("That message is not flagged for review.");

        var parties = await moderation.FindPartiesAsync(command.MessageId, cancellationToken)
            ?? throw new NotFoundException("That message is not flagged for review.");

        var utcNow = clock.UtcNow;

        message.Moderate(command.Outcome, userId, utcNow);

        if (command.Outcome == MessageModerationOutcome.Escalated)
        {
            /*
             * Escalation raises an ordinary incident rather than inventing a second place
             * where decisions about someone's account get made. It lands in the queue that
             * already has assignment, resolution and an audit trail.
             *
             * The staff member's notes are the description; the message body is not copied
             * into it. An incident is read by more people and lives longer than this
             * review, and the body stays where it was written.
             */
            var report = IncidentReport.Raise(
                userId,
                parties.SenderUserId,
                careBookingId: null,
                IncidentCategory.InappropriateConduct,
                IncidentSeverity.Low,
                command.Notes!,
                utcNow);

            incidents.Add(report);
        }

        audit.Record(nameof(Message), message.Id, $"MessageModerated:{command.Outcome}", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Flagged message {MessageId} was settled as {Outcome} by {UserId}",
            message.Id,
            command.Outcome,
            userId);

        return Unit.Value;
    }
}
