namespace GoSit.Application.Features.Subscriptions;

/// <summary>
/// The numbers that turn a feature code into a rule.
///
/// A code like <c>UNLIMITED_FAVOURITES</c> says nothing about what the *limit* is, and
/// <c>REDUCED_COMMISSION</c> says nothing about the reduction. Enforcement needs those
/// numbers, so they are gathered here rather than scattered across the handlers that
/// apply them.
///
/// OPEN BUSINESS DECISION — every value below is a placeholder. PLAN.md records that
/// plan pricing and packaging are product decisions; these are the engineering
/// consequence of that gap, chosen to be defensible rather than correct. Changing what
/// a plan is worth should be an edit to this file, not a hunt through features.
/// </summary>
public static class EntitlementPolicy
{
    /// <summary>
    /// Caregivers a requester may save without <see cref="SubscriptionFeature"/>'s
    /// unlimited favourites.
    ///
    /// Set high enough that it is not a nuisance during ordinary use — someone
    /// comparing caregivers for a parent and a child at once can easily hold a dozen —
    /// and low enough that "unlimited" means something.
    /// </summary>
    public const int FreeFavouriteLimit = 25;

    /// <summary>
    /// Search filters sold as advanced.
    ///
    /// Taken from the feature's own description ("filter by certification, language and
    /// experience") rather than invented here. Location, service, availability, price
    /// and page size stay free: they are how anyone finds care at all, and putting them
    /// behind a plan would make the free product not a product.
    ///
    /// Rating is deliberately NOT in this list, despite appearing in the feature's older
    /// wording. Hiding "only show well-reviewed caregivers" behind a payment in a care
    /// marketplace sells safety back to families, and that is not a trade GoSit should
    /// make.
    /// </summary>
    public const string AdvancedFilterDescription =
        "Filtering by certification and experience is part of a paid plan.";

    /// <summary>
    /// How far a promoted caregiver may move up the results.
    ///
    /// A promoted profile is ranked ahead of others *at the same distance band*, and
    /// never ahead of eligibility, verification or proximity. GoSit ranks people who
    /// will look after a child or a parent; letting a subscription outrank suitability
    /// would sell a family a worse match, which is a different product from the one
    /// described on the plan screen. See <c>CaregiverRanking</c>.
    /// </summary>
    public const double PromotionDistanceBandKm = 5.0;
}
