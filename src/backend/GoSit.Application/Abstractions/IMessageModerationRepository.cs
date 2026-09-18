using GoSit.Domain.Messaging;

namespace GoSit.Application.Abstractions;

/// <summary>
/// The moderation queue for messages flagged as possible contact sharing.
///
/// Reading it means reading private messages, which everything else in this codebase is
/// built to prevent. Two rules follow from that, and they are the reason this is a
/// separate repository rather than a filter on the messaging one:
///
/// - <see cref="ListAwaitingModerationAsync"/> returns **no message bodies**. A staff
///   member can see that something was flagged, between whom, and when, without reading
///   anything either of them wrote.
/// - Reading a body needs <see cref="FindForReviewAsync"/>, a deliberate second act on
///   one message, which the handler audits.
///
/// A queue that rendered every flagged body in a list would make browsing strangers'
/// private conversations the default way to use this screen.
/// </summary>
public interface IMessageModerationRepository
{
    Task<IReadOnlyList<FlaggedMessageRow>> ListAwaitingModerationAsync(
        int take,
        CancellationToken cancellationToken);

    /// <summary>How many are waiting, for the queue badge.</summary>
    Task<int> CountAwaitingModerationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One flagged message, tracked, for a staff member to read and decide on. Returns
    /// null when the message does not exist or was never flagged.
    /// </summary>
    Task<Message?> FindForReviewAsync(Guid messageId, CancellationToken cancellationToken);

    /// <summary>
    /// The two people in the conversation the message belongs to, so an escalation knows
    /// who it is about and a reviewer knows who they are reading.
    /// </summary>
    Task<FlaggedMessageParties?> FindPartiesAsync(
        Guid messageId,
        CancellationToken cancellationToken);
}

/// <summary>
/// A row in the queue. Carries who, when, and why it was flagged — never what was said.
/// </summary>
public sealed record FlaggedMessageRow(
    Guid MessageId,
    Guid ConversationId,
    string SenderName,
    string RecipientName,

    /// <summary>Which side sent it, so staff can see the direction without opening it.</summary>
    bool SentByCaregiver,

    DateTime SentAtUtc);

/// <summary>
/// The user ids behind a flagged message. Kept out of <see cref="FlaggedMessageRow"/>
/// because the queue has no use for them and an escalation does.
/// </summary>
public sealed record FlaggedMessageParties(
    Guid SenderUserId,
    Guid RecipientUserId,
    string SenderName,
    string RecipientName);
