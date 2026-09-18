using GoSit.Domain.Common;

namespace GoSit.Domain.Subscriptions.Events;

/// <summary>
/// Subscription domain events. Identifiers, plan codes and amounts only — never card
/// details or a provider secret.
/// </summary>
public sealed record SubscriptionStartedDomainEvent(
    Guid SubscriptionId,
    Guid UserId,
    string PlanCode,
    decimal MonthlyPrice,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record SubscriptionRenewedDomainEvent(
    Guid SubscriptionId,
    Guid UserId,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record SubscriptionCancelledDomainEvent(
    Guid SubscriptionId,
    Guid UserId,
    DateTime EffectiveAtUtc,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record SubscriptionEndedDomainEvent(
    Guid SubscriptionId,
    Guid UserId,
    string Reason,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record ReferralQualifiedDomainEvent(
    Guid ReferralId,
    Guid ReferrerUserId,
    Guid RefereeUserId,
    DateTime OccurredAtUtc) : IDomainEvent;
