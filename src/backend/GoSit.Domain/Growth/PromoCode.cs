using GoSit.Domain.Common;
using GoSit.Domain.Payments;
using GoSit.Domain.Subscriptions;

namespace GoSit.Domain.Growth;

public enum DiscountType
{
    /// <summary>A percentage off, e.g. 25 for 25%.</summary>
    Percentage = 1,

    /// <summary>A fixed amount off, in CAD.</summary>
    FixedAmount = 2,
}

/// <summary>
/// A promotional code.
///
/// Every reason a code can be refused is checked here rather than in a handler, because
/// a promo code is a way of giving money away and the checks are the product: expiry, a
/// redemption cap, the right audience, and one use per account.
///
/// <see cref="RedemptionCount"/> is incremented through <see cref="Redeem"/> only, and
/// the cap is enforced in the same call, so a race cannot overspend the budget beyond
/// the concurrency token.
/// </summary>
public sealed class PromoCode : Entity
{
    private PromoCode()
    {
    }

    public string Code { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public DiscountType DiscountType { get; private set; }

    /// <summary>Percent off, or an amount in CAD, depending on the type.</summary>
    public decimal Value { get; private set; }

    /// <summary>Which side of the marketplace can use it. Null means anyone.</summary>
    public PlanAudience? Audience { get; private set; }

    public DateTime ValidFromUtc { get; private set; }

    public DateTime? ValidUntilUtc { get; private set; }

    /// <summary>Total redemptions allowed. Null means unlimited.</summary>
    public int? MaxRedemptions { get; private set; }

    public int RedemptionCount { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int Version { get; private set; }

    public static PromoCode Create(
        string code,
        string description,
        DiscountType discountType,
        decimal value,
        DateTime validFromUtc,
        DateTime? validUntilUtc,
        int? maxRedemptions,
        PlanAudience? audience)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("A promo code is required.");
        }

        if (value <= 0)
        {
            throw new DomainException("A discount must be positive.");
        }

        if (discountType == DiscountType.Percentage && value > 100)
        {
            throw new DomainException("A percentage discount cannot exceed 100.");
        }

        if (discountType == DiscountType.FixedAmount)
        {
            // Rejects a discount that is not a whole number of cents.
            _ = Money.ToMinorUnits(value);
        }

        if (validUntilUtc is { } until && until <= validFromUtc)
        {
            throw new DomainException("A promo code must expire after it becomes valid.");
        }

        if (maxRedemptions is <= 0)
        {
            throw new DomainException("A redemption cap must be positive.");
        }

        return new PromoCode
        {
            Code = ReferralCode.Normalize(code),
            Description = description.Trim(),
            DiscountType = discountType,
            Value = value,
            ValidFromUtc = validFromUtc,
            ValidUntilUtc = validUntilUtc,
            MaxRedemptions = maxRedemptions,
            Audience = audience,
        };
    }

    /// <summary>
    /// Why this code cannot be used right now, or null if it can. Returned as a reason
    /// rather than a bool so the caller can tell someone what went wrong.
    /// </summary>
    public string? RejectionReason(DateTime utcNow, PlanAudience audience)
    {
        if (!IsActive)
        {
            return "That promo code is no longer available.";
        }

        if (utcNow < ValidFromUtc)
        {
            return "That promo code is not active yet.";
        }

        if (ValidUntilUtc is { } until && utcNow > until)
        {
            return "That promo code has expired.";
        }

        if (MaxRedemptions is { } cap && RedemptionCount >= cap)
        {
            return "That promo code has been fully redeemed.";
        }

        if (Audience is { } required && required != audience)
        {
            // Deliberately not "this code is for caregivers" — that would leak the
            // campaign's targeting to anyone who tried a code.
            return "That promo code cannot be used on this account.";
        }

        return null;
    }

    /// <summary>
    /// The amount off a given price. Never more than the price itself, so a discount
    /// cannot produce a negative charge.
    /// </summary>
    public decimal DiscountOn(decimal amount)
    {
        if (amount <= 0)
        {
            return 0m;
        }

        var discount = DiscountType == DiscountType.Percentage
            ? Money.Round(amount * (Value / 100m))
            : Value;

        return Math.Min(discount, amount);
    }

    public void Redeem(DateTime utcNow, PlanAudience audience)
    {
        if (RejectionReason(utcNow, audience) is { } reason)
        {
            throw new DomainException(reason);
        }

        RedemptionCount++;
        Version++;
    }

    public void Deactivate()
    {
        IsActive = false;
        Version++;
    }
}

/// <summary>
/// A record that one account used one code. The unique index on
/// (PromoCodeId, UserId) is what actually enforces one use per account.
/// </summary>
public sealed class PromoRedemption : Entity
{
    private PromoRedemption()
    {
    }

    public Guid PromoCodeId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>What the discount was worth, captured at redemption.</summary>
    public decimal DiscountAmount { get; private set; }

    public DateTime RedeemedAtUtc { get; private set; }

    public static PromoRedemption Create(
        Guid promoCodeId,
        Guid userId,
        decimal discountAmount,
        DateTime utcNow) =>
        new()
        {
            PromoCodeId = promoCodeId,
            UserId = userId,
            DiscountAmount = discountAmount,
            RedeemedAtUtc = utcNow,
        };
}
