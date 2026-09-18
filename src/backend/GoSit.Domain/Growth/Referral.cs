using GoSit.Domain.Common;
using GoSit.Domain.Subscriptions.Events;

namespace GoSit.Domain.Growth;

public enum ReferralStatus
{
    /// <summary>The referee signed up but has not yet done anything that counts.</summary>
    Pending = 1,

    /// <summary>The referee completed a booking. The reward is now owed.</summary>
    Qualified = 2,

    /// <summary>The reward has been granted.</summary>
    Rewarded = 3,

    /// <summary>Voided — abuse, or an account removed.</summary>
    Void = 4,
}

/// <summary>
/// One person referring another.
///
/// The rule that makes this workable is when a referral <i>qualifies</i>: not at signup,
/// but when the referee actually completes a booking. Rewarding signups would pay for
/// throwaway accounts, and a referral programme that pays for nothing is a fraud budget.
///
/// A referral is recorded once per referee, ever. Someone cannot be referred twice, and
/// cannot refer themselves.
/// </summary>
public sealed class Referral : Entity
{
    private Referral()
    {
    }

    public Guid ReferrerUserId { get; private set; }

    public Guid RefereeUserId { get; private set; }

    /// <summary>The code as entered, normalized. Kept for support and abuse review.</summary>
    public string Code { get; private set; } = string.Empty;

    public ReferralStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? QualifiedAtUtc { get; private set; }

    public DateTime? RewardedAtUtc { get; private set; }

    /// <summary>What the referrer receives. Null until the reward is granted.</summary>
    public decimal? RewardAmount { get; private set; }

    public int Version { get; private set; }

    public static Referral Record(
        Guid referrerUserId,
        Guid refereeUserId,
        string code,
        DateTime utcNow)
    {
        if (referrerUserId == Guid.Empty || refereeUserId == Guid.Empty)
        {
            throw new DomainException("Both accounts are required.");
        }

        // The most obvious abuse, and the cheapest to stop.
        if (referrerUserId == refereeUserId)
        {
            throw new DomainException("You cannot refer yourself.");
        }

        return new Referral
        {
            ReferrerUserId = referrerUserId,
            RefereeUserId = refereeUserId,
            Code = ReferralCode.Normalize(code),
            Status = ReferralStatus.Pending,
            CreatedAtUtc = utcNow,
        };
    }

    /// <summary>
    /// Called when the referee completes their first booking. Idempotent, because the
    /// caller may see more than one completion.
    /// </summary>
    public bool Qualify(DateTime utcNow)
    {
        if (Status != ReferralStatus.Pending)
        {
            return false;
        }

        Status = ReferralStatus.Qualified;
        QualifiedAtUtc = utcNow;
        Version++;

        RaiseDomainEvent(new ReferralQualifiedDomainEvent(
            Id, ReferrerUserId, RefereeUserId, utcNow));

        return true;
    }

    public void Reward(decimal amount, DateTime utcNow)
    {
        if (Status != ReferralStatus.Qualified)
        {
            throw new DomainException("Only a qualified referral can be rewarded.");
        }

        if (amount <= 0)
        {
            throw new DomainException("A reward must be positive.");
        }

        Status = ReferralStatus.Rewarded;
        RewardAmount = amount;
        RewardedAtUtc = utcNow;
        Version++;
    }

    /// <summary>Voids a referral found to be abusive. A rewarded one is left alone.</summary>
    public void Void()
    {
        if (Status == ReferralStatus.Rewarded)
        {
            throw new DomainException("A rewarded referral cannot be voided; reverse the reward.");
        }

        Status = ReferralStatus.Void;
        Version++;
    }
}
