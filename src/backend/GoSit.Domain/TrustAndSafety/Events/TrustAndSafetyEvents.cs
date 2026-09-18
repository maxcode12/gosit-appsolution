using GoSit.Domain.Common;

namespace GoSit.Domain.TrustAndSafety.Events;

/// <summary>
/// Raised when a user reports another. Carries identifiers and the classification only —
/// the description is sensitive and never travels on an event or through the outbox.
/// </summary>
public sealed record IncidentReportedDomainEvent(
    Guid IncidentReportId,
    Guid SubjectUserId,
    IncidentCategory Category,
    IncidentSeverity Severity,
    bool RequiresUrgentReview,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record CaregiverVerifiedDomainEvent(
    Guid CaregiverProfileId,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record AccountSuspendedDomainEvent(
    Guid UserId,
    DateTime OccurredAtUtc) : IDomainEvent;
