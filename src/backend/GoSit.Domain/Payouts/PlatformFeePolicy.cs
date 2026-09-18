using GoSit.Domain.Common;
using GoSit.Domain.Payments;

namespace GoSit.Domain.Payouts;

/// <summary>
/// Splits a booking payment between the caregiver and GoSit.
///
/// OPEN BUSINESS DECISION: the commission rate below is a placeholder, not an agreed
/// price. It is recorded in docs/PLAN.md as needing a decision before launch — it
/// determines what caregivers actually earn and cannot be settled by engineering.
///
/// The rate is captured on each payout when it is created, so changing it later never
/// alters what an already-earned payout is worth.
/// </summary>
public sealed class PlatformFeePolicy
{
    /// <summary>Placeholder commission. See the note above.</summary>
    public const decimal DefaultCommissionRate = 0.15m;

    /// <summary>
    /// Placeholder commission for a caregiver whose plan includes
    /// <c>REDUCED_COMMISSION</c>. A third off the standard rate, which is the whole
    /// reason a caregiver would buy the plan and equally a placeholder.
    /// </summary>
    public const decimal DefaultReducedCommissionRate = 0.10m;

    public PlatformFeePolicy(decimal commissionRate, decimal reducedCommissionRate)
    {
        if (commissionRate is < 0m or > 0.5m)
        {
            throw new DomainException("A commission rate must be between 0 and 50 percent.");
        }

        if (reducedCommissionRate is < 0m or > 0.5m)
        {
            throw new DomainException("A commission rate must be between 0 and 50 percent.");
        }

        // A "reduced" rate that costs more is a configuration mistake that would quietly
        // charge subscribers extra for the privilege of subscribing.
        if (reducedCommissionRate > commissionRate)
        {
            throw new DomainException(
                "The reduced commission rate cannot exceed the standard rate.");
        }

        CommissionRate = commissionRate;
        ReducedCommissionRate = reducedCommissionRate;
    }

    public decimal CommissionRate { get; }

    public decimal ReducedCommissionRate { get; }

    /// <summary>The rate that applies to one caregiver, given their entitlements.</summary>
    public decimal RateFor(bool hasReducedCommission) =>
        hasReducedCommission ? ReducedCommissionRate : CommissionRate;

    /// <summary>
    /// The caregiver's share of an amount collected. GoSit's fee is the remainder, so
    /// the two always add back to the total and no cent is created or lost in rounding.
    /// </summary>
    public FeeSplit Split(decimal grossAmount, bool hasReducedCommission = false)
    {
        if (grossAmount < 0)
        {
            throw new DomainException("A gross amount cannot be negative.");
        }

        var fee = Money.Round(grossAmount * RateFor(hasReducedCommission));
        return new FeeSplit(grossAmount, fee, grossAmount - fee);
    }
}

/// <summary>
/// How one collected amount divides. <see cref="Gross"/> always equals
/// <see cref="PlatformFee"/> plus <see cref="CaregiverEarnings"/>.
/// </summary>
public sealed record FeeSplit(decimal Gross, decimal PlatformFee, decimal CaregiverEarnings);
