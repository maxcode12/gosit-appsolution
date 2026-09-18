namespace GoSit.Domain.Subscriptions;

/// <summary>
/// Who a plan is sold to. A care requester and a caregiver buy different things, and a
/// plan must never be sold to the wrong side of the marketplace.
/// </summary>
public enum PlanAudience
{
    CareRequester = 1,
    Caregiver = 2,
}

/// <summary>
/// Capabilities a plan can unlock.
///
/// OPEN BUSINESS DECISION: which features belong to which plan — and whether these are
/// the right features at all — is a product and pricing question, not an engineering
/// one. The set below is a placeholder so entitlement checks have something concrete to
/// resolve; see docs/PLAN.md.
///
/// What is NOT negotiable is where entitlements are decided: always on the server, from
/// the stored subscription. A client can never assert that it is entitled to something.
/// </summary>
public static class SubscriptionFeature
{
    /// <summary>Save more than the free number of caregivers.</summary>
    public const string UnlimitedFavourites = "UNLIMITED_FAVOURITES";

    /// <summary>Filter search by certification, rating and experience.</summary>
    public const string AdvancedSearchFilters = "ADVANCED_SEARCH_FILTERS";

    /// <summary>Bookings are shown to caregivers ahead of the standard queue.</summary>
    public const string PriorityBookingRequests = "PRIORITY_BOOKING_REQUESTS";

    /// <summary>A caregiver's profile is boosted in search results.</summary>
    public const string ProfilePromotion = "PROFILE_PROMOTION";

    /// <summary>See how a profile is performing in search and bookings.</summary>
    public const string CaregiverInsights = "CAREGIVER_INSIGHTS";

    /// <summary>Reduced platform commission on payouts.</summary>
    public const string ReducedCommission = "REDUCED_COMMISSION";

    public static readonly string[] All =
    [
        UnlimitedFavourites,
        AdvancedSearchFilters,
        PriorityBookingRequests,
        ProfilePromotion,
        CaregiverInsights,
        ReducedCommission,
    ];
}
