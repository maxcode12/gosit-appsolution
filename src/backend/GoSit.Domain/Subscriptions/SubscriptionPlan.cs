using GoSit.Domain.Common;
using GoSit.Domain.Payments;

namespace GoSit.Domain.Subscriptions;

/// <summary>
/// A plan someone can be on. Reference data, seeded and configurable, because pricing
/// and packaging change far more often than code should.
///
/// Every audience has exactly one free plan, which is what an account is on when it has
/// no subscription at all. That keeps "not subscribed" and "on the free plan" the same
/// thing, so entitlement checks never have a null case to get wrong.
/// </summary>
public sealed class SubscriptionPlan : Entity
{
    private readonly List<string> _features = [];

    private SubscriptionPlan()
    {
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public PlanAudience Audience { get; private set; }

    /// <summary>Monthly price in CAD. Zero for the free plan.</summary>
    public decimal MonthlyPrice { get; private set; }

    public string Currency { get; private set; } = Money.CanadianDollar;

    public bool IsFree => MonthlyPrice == 0m;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyList<string> Features => _features.AsReadOnly();

    /// <summary>
    /// The billing provider's price for this plan — a Stripe Price id.
    ///
    /// Null until billing is configured, and null forever on a free plan. It lives on
    /// the row rather than in code because the price a customer is charged is Stripe's
    /// record, not ours: <see cref="MonthlyPrice"/> is what we display, and the two are
    /// kept in step by whoever sets up the Stripe product.
    /// </summary>
    public string? ProviderPriceId { get; private set; }

    /// <summary>Whether this plan can actually be bought through the provider.</summary>
    public bool IsPurchasable => !IsFree && !string.IsNullOrWhiteSpace(ProviderPriceId);

    public bool Includes(string featureCode) =>
        _features.Contains(featureCode, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Points this plan at a provider price, or clears it. Idempotent, so re-running
    /// configuration on every boot is safe.
    /// </summary>
    public void LinkProviderPrice(string? providerPriceId)
    {
        if (IsFree && !string.IsNullOrWhiteSpace(providerPriceId))
        {
            // A free plan is the absence of a subscription. Giving it a price would
            // make it purchasable, which would create a paid row for a free state.
            throw new DomainException("A free plan cannot have a provider price.");
        }

        ProviderPriceId = string.IsNullOrWhiteSpace(providerPriceId)
            ? null
            : providerPriceId.Trim();
    }

    public static SubscriptionPlan Create(
        string code,
        string name,
        PlanAudience audience,
        decimal monthlyPrice,
        int displayOrder,
        IEnumerable<string> features)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("A plan code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A plan name is required.");
        }

        if (monthlyPrice < 0)
        {
            throw new DomainException("A plan price cannot be negative.");
        }

        // Rejects a price that is not a whole number of cents before it can ever reach
        // a payment provider.
        _ = Money.ToMinorUnits(monthlyPrice);

        var plan = new SubscriptionPlan
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Audience = audience,
            MonthlyPrice = monthlyPrice,
            DisplayOrder = displayOrder,
        };

        plan._features.AddRange(
            features
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => f.Trim().ToUpperInvariant())
                .Distinct());

        return plan;
    }
}
