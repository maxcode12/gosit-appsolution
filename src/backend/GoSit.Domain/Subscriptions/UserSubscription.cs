using GoSit.Domain.Common;
using GoSit.Domain.Subscriptions.Events;

namespace GoSit.Domain.Subscriptions;

public enum SubscriptionStatus
{
    Active = 1,

    /// <summary>Payment failed. Entitlements continue through a grace period.</summary>
    PastDue = 2,

    /// <summary>Cancelled and past its paid period. Entitlements are gone.</summary>
    Cancelled = 3,

    /// <summary>Lapsed after payment kept failing.</summary>
    Expired = 4,
}

/// <summary>
/// What one account is currently paying for.
///
/// Two rules matter more than the rest:
///
/// - entitlements come from <see cref="IsActiveAt"/>, evaluated against stored state and
///   the clock — never from anything a client sends;
/// - a paid period that has been paid for is honoured to its end. Cancelling stops the
///   renewal, it does not confiscate the remainder.
/// </summary>
public sealed class UserSubscription : Entity
{
    /// <summary>
    /// How long entitlements survive a failed payment. Cutting a family off from the
    /// caregivers they rely on the instant a card expires would be the wrong trade.
    /// </summary>
    public static readonly TimeSpan PastDueGrace = TimeSpan.FromDays(7);

    private UserSubscription()
    {
    }

    public Guid UserId { get; private set; }

    public Guid SubscriptionPlanId { get; private set; }

    public SubscriptionPlan Plan { get; private set; } = null!;

    public SubscriptionStatus Status { get; private set; }

    public DateTime CurrentPeriodStartUtc { get; private set; }

    /// <summary>When the paid period runs out. Renewal extends it.</summary>
    public DateTime CurrentPeriodEndUtc { get; private set; }

    /// <summary>Set when someone cancels: they keep what they paid for until the period ends.</summary>
    public bool CancelAtPeriodEnd { get; private set; }

    /// <summary>The provider's subscription reference, once billing is wired up.</summary>
    public string? ProviderSubscriptionId { get; private set; }

    /// <summary>When payment first failed. The grace period runs from here.</summary>
    public DateTime? PastDueSinceUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public int Version { get; private set; }

    public static UserSubscription Start(
        Guid userId,
        SubscriptionPlan plan,
        DateTime utcNow,
        string? providerSubscriptionId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("An account is required.");
        }

        if (plan.IsFree)
        {
            // The free plan is the absence of a subscription, not a row. Storing one
            // would create two ways to express the same state.
            throw new DomainException("The free plan does not need a subscription.");
        }

        var subscription = new UserSubscription
        {
            UserId = userId,
            SubscriptionPlanId = plan.Id,
            Plan = plan,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStartUtc = utcNow,
            CurrentPeriodEndUtc = utcNow.AddMonths(1),
            ProviderSubscriptionId = providerSubscriptionId,
            CreatedAtUtc = utcNow,
        };

        subscription.RaiseDomainEvent(new SubscriptionStartedDomainEvent(
            subscription.Id, userId, plan.Code, plan.MonthlyPrice, utcNow));

        return subscription;
    }

    /// <summary>
    /// Whether this subscription entitles the account to its plan's features right now.
    ///
    /// The single source of truth for entitlement. Past due still counts, but only
    /// inside the grace period.
    /// </summary>
    public bool IsActiveAt(DateTime utcNow) => Status switch
    {
        SubscriptionStatus.Active => utcNow < CurrentPeriodEndUtc,
        SubscriptionStatus.PastDue => PastDueSinceUtc is { } since
            && utcNow - since <= PastDueGrace,
        _ => false,
    };

    /// <summary>
    /// Adopts the provider's own billing period.
    ///
    /// Once Stripe is billing a subscription, Stripe's period end is the truth — it
    /// knows about proration, trials, dunning retries and clock skew, and our
    /// <c>AddMonths(1)</c> guess knows about none of them. Entitlement is decided from
    /// this date, so letting the two drift would mean charging for a month and
    /// entitling a different one.
    /// </summary>
    public void SyncBillingPeriod(DateTime periodStartUtc, DateTime periodEndUtc)
    {
        if (periodEndUtc <= periodStartUtc)
        {
            throw new DomainException("A billing period must end after it starts.");
        }

        CurrentPeriodStartUtc = periodStartUtc;
        CurrentPeriodEndUtc = periodEndUtc;
        Version++;
    }

    /// <summary>Records the provider's subscription reference after checkout completes.</summary>
    public void LinkProviderSubscription(string providerSubscriptionId)
    {
        if (string.IsNullOrWhiteSpace(providerSubscriptionId))
        {
            throw new DomainException("A provider subscription id is required.");
        }

        ProviderSubscriptionId = providerSubscriptionId;
        Version++;
    }

    /// <summary>
    /// Marks a past-due subscription healthy again after a retried payment succeeded.
    /// Distinct from <see cref="Renew"/>, which also advances the period — a recovered
    /// payment for the *current* period must not buy an extra month.
    /// </summary>
    public void MarkPaymentRecovered()
    {
        if (Status != SubscriptionStatus.PastDue)
        {
            return;
        }

        Status = SubscriptionStatus.Active;
        PastDueSinceUtc = null;
        Version++;
    }

    public void Renew(DateTime utcNow)
    {
        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
        {
            throw new DomainException($"A subscription that is {Status} cannot renew.");
        }

        if (CancelAtPeriodEnd)
        {
            // Cancelled but still inside its paid period. Renewal must not resurrect it.
            throw new DomainException("This subscription is set to end and cannot renew.");
        }

        // Extends from the period end, not from now, so a late renewal does not silently
        // shorten what was paid for.
        CurrentPeriodStartUtc = CurrentPeriodEndUtc;
        CurrentPeriodEndUtc = CurrentPeriodEndUtc.AddMonths(1);
        Status = SubscriptionStatus.Active;
        PastDueSinceUtc = null;
        Version++;

        RaiseDomainEvent(new SubscriptionRenewedDomainEvent(Id, UserId, utcNow));
    }

    public void MarkPastDue(DateTime utcNow)
    {
        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
        {
            return;
        }

        // Idempotent: repeated failure notices must not keep resetting the grace clock.
        if (Status == SubscriptionStatus.PastDue)
        {
            return;
        }

        Status = SubscriptionStatus.PastDue;
        PastDueSinceUtc = utcNow;
        Version++;
    }

    /// <summary>
    /// Stops the renewal but keeps entitlements to the end of the paid period. Someone
    /// who has paid for a month gets the month.
    /// </summary>
    public void CancelAtEndOfPeriod(DateTime utcNow)
    {
        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
        {
            throw new DomainException($"A subscription that is {Status} cannot be cancelled.");
        }

        CancelAtPeriodEnd = true;
        CancelledAtUtc = utcNow;
        Version++;

        RaiseDomainEvent(new SubscriptionCancelledDomainEvent(
            Id, UserId, CurrentPeriodEndUtc, utcNow));
    }

    /// <summary>Ends it now. For staff use — a refund, or an account being removed.</summary>
    public void CancelImmediately(DateTime utcNow)
    {
        Status = SubscriptionStatus.Cancelled;
        CancelAtPeriodEnd = true;
        CancelledAtUtc = utcNow;
        CurrentPeriodEndUtc = utcNow;
        Version++;

        RaiseDomainEvent(new SubscriptionCancelledDomainEvent(Id, UserId, utcNow, utcNow));
    }

    /// <summary>
    /// Closes out a subscription whose period has run out — either cancelled and
    /// expired, or past due beyond its grace. Called by the scheduled sweep.
    /// </summary>
    public bool ExpireIfDue(DateTime utcNow)
    {
        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
        {
            return false;
        }

        if (IsActiveAt(utcNow))
        {
            return false;
        }

        Status = CancelAtPeriodEnd ? SubscriptionStatus.Cancelled : SubscriptionStatus.Expired;
        Version++;

        RaiseDomainEvent(new SubscriptionEndedDomainEvent(Id, UserId, Status.ToString(), utcNow));
        return true;
    }
}
