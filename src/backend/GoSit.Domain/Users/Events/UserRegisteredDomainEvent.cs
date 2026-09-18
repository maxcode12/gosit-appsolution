using GoSit.Domain.Common;

namespace GoSit.Domain.Users.Events;

/// <summary>
/// Raised when a new account is created. Carries identifiers only — downstream
/// consumers look up whatever else they need, so no personal data travels on the event.
/// </summary>
public sealed record UserRegisteredDomainEvent(
    Guid UserId,
    UserRole Role,
    DateTime OccurredAtUtc) : IDomainEvent;
