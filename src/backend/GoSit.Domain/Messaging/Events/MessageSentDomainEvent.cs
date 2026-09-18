using GoSit.Domain.Common;

namespace GoSit.Domain.Messaging.Events;

/// <summary>
/// Raised when a message is sent. Identifiers only — the body never travels on an event,
/// through the outbox, or into a notification.
/// </summary>
public sealed record MessageSentDomainEvent(
    Guid ConversationId,
    Guid MessageId,
    Guid SenderProfileId,
    Guid RecipientProfileId,
    bool FlaggedForReview,
    DateTime OccurredAtUtc) : IDomainEvent;
