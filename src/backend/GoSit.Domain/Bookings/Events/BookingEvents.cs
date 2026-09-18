using GoSit.Domain.Common;

namespace GoSit.Domain.Bookings.Events;

/// <summary>
/// Domain events raised by <see cref="CareBooking"/>.
///
/// Identifiers only. Consumers look up whatever else they need, so no care needs, no
/// address, and no personal details ever travel on an event or through the outbox.
/// </summary>
public sealed record BookingRequestedDomainEvent(
    Guid BookingId,
    Guid CareRequesterProfileId,
    Guid CaregiverProfileId,
    Guid CareServiceId,
    DateTime StartAtUtc,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record BookingAcceptedDomainEvent(
    Guid BookingId,
    Guid CareRequesterProfileId,
    Guid CaregiverProfileId,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record BookingDeclinedDomainEvent(
    Guid BookingId,
    Guid CareRequesterProfileId,
    Guid CaregiverProfileId,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record BookingCancelledDomainEvent(
    Guid BookingId,
    Guid CareRequesterProfileId,
    Guid CaregiverProfileId,
    CancellationParty CancelledBy,
    bool IsLateCancellation,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record BookingStartedDomainEvent(
    Guid BookingId,
    Guid CareRequesterProfileId,
    Guid CaregiverProfileId,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record BookingCompletedDomainEvent(
    Guid BookingId,
    Guid CareRequesterProfileId,
    Guid CaregiverProfileId,
    decimal Total,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record BookingDisputedDomainEvent(
    Guid BookingId,
    Guid CareRequesterProfileId,
    Guid CaregiverProfileId,
    DateTime OccurredAtUtc) : IDomainEvent;
