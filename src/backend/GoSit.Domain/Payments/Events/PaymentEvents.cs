using GoSit.Domain.Common;

namespace GoSit.Domain.Payments.Events;

/// <summary>
/// Payment domain events. Identifiers and amounts only — never a card detail, a client
/// secret, or a provider API key.
/// </summary>
public sealed record PaymentSucceededDomainEvent(
    Guid PaymentId,
    Guid CareBookingId,
    Guid CareRequesterProfileId,
    Guid CaregiverProfileId,
    decimal Amount,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record PaymentFailedDomainEvent(
    Guid PaymentId,
    Guid CareBookingId,
    Guid CareRequesterProfileId,
    string? FailureCode,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record RefundCompletedDomainEvent(
    Guid PaymentId,
    Guid CareBookingId,
    Guid CareRequesterProfileId,
    decimal Amount,
    bool IsFullRefund,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record PayoutQueuedDomainEvent(
    Guid PayoutId,
    Guid CaregiverProfileId,
    decimal Amount,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record PayoutCompletedDomainEvent(
    Guid PayoutId,
    Guid CaregiverProfileId,
    decimal Amount,
    DateTime OccurredAtUtc) : IDomainEvent;
