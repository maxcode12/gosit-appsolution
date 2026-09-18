using GoSit.Domain.Common;

namespace GoSit.Domain.Reviews.Events;

/// <summary>
/// Raised when a review becomes visible to the person it is about — not when it is
/// written. Identifiers and the rating only; the comment never travels on an event.
/// </summary>
public sealed record ReviewPublishedDomainEvent(
    Guid ReviewId,
    Guid CareBookingId,
    Guid AuthorProfileId,
    Guid SubjectProfileId,
    bool SubjectIsCaregiver,
    int Rating,
    DateTime OccurredAtUtc) : IDomainEvent;
