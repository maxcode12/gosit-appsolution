using GoSit.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Persistence;

/// <summary>
/// Seeds the subscription plans.
///
/// OPEN BUSINESS DECISION: the prices and the feature packaging below are placeholders
/// so entitlement checks resolve against something concrete. What Plus costs and what it
/// includes is a product and pricing question — see docs/PLAN.md.
///
/// Every audience gets exactly one free plan, because "no subscription" and "free plan"
/// must be the same state.
/// </summary>
public sealed partial class ReferenceDataSeeder
{
    private sealed record PlanDefinition(
        string Code,
        string Name,
        PlanAudience Audience,
        decimal MonthlyPrice,
        int DisplayOrder,
        string[] Features);

    private static readonly PlanDefinition[] Plans =
    [
        new("FREE_REQUESTER", "GoSit", PlanAudience.CareRequester, 0m, 1, []),
        new("PLUS_REQUESTER", "GoSit Plus", PlanAudience.CareRequester, 12m, 2,
        [
            SubscriptionFeature.UnlimitedFavourites,
            SubscriptionFeature.AdvancedSearchFilters,
            SubscriptionFeature.PriorityBookingRequests,
        ]),

        new("FREE_CAREGIVER", "GoSit", PlanAudience.Caregiver, 0m, 1, []),
        new("PRO_CAREGIVER", "GoSit Pro", PlanAudience.Caregiver, 19m, 2,
        [
            SubscriptionFeature.ProfilePromotion,
            SubscriptionFeature.CaregiverInsights,
            SubscriptionFeature.ReducedCommission,
        ]),
    ];

    private async Task SeedSubscriptionPlansAsync(CancellationToken cancellationToken)
    {
        var plans = await context.SubscriptionPlans.ToListAsync(cancellationToken);
        var known = plans.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var definition in Plans)
        {
            if (known.ContainsKey(definition.Code))
            {
                continue;
            }

            var plan = SubscriptionPlan.Create(
                definition.Code,
                definition.Name,
                definition.Audience,
                definition.MonthlyPrice,
                definition.DisplayOrder,
                definition.Features);

            context.SubscriptionPlans.Add(plan);
            known[plan.Code] = plan;
            added++;
        }

        var linked = LinkProviderPrices(known);

        if (added > 0 || linked > 0)
        {
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Seeded {Added} subscription plans and linked {Linked} provider prices",
                added,
                linked);
        }
    }

    /// <summary>
    /// Points each paid plan at its Stripe Price, from configuration:
    /// <c>Stripe:Prices:PLUS_REQUESTER = price_...</c>
    ///
    /// Configuration rather than a constant, because a Price id differs between Stripe's
    /// test and live modes and between anyone's account — hard-coding one would make the
    /// repository unusable by a second environment.
    ///
    /// Re-applied on every boot so changing the configuration is enough to repoint a
    /// plan; a plan with nothing configured is left unlinked and simply reports itself
    /// as not purchasable.
    /// </summary>
    private int LinkProviderPrices(IReadOnlyDictionary<string, SubscriptionPlan> plans)
    {
        var configured = configuration.GetSection("Stripe:Prices").GetChildren();
        var linked = 0;

        foreach (var entry in configured)
        {
            if (string.IsNullOrWhiteSpace(entry.Value)
                || !plans.TryGetValue(entry.Key, out var plan)
                || plan.ProviderPriceId == entry.Value)
            {
                continue;
            }

            plan.LinkProviderPrice(entry.Value);
            linked++;
        }

        return linked;
    }
}
