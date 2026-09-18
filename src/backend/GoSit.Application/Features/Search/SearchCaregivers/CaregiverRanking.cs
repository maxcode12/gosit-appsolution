using GoSit.Domain.Caregivers;
using GoSit.Domain.Common.ValueObjects;

namespace GoSit.Application.Features.Search.SearchCaregivers;

/// <summary>
/// A candidate with everything ranking needs already computed, so the comparison does
/// no work per call.
/// </summary>
public sealed record RankedCaregiver(
    CaregiverProfile Profile,
    bool MatchesAvailability,
    double? DistanceKm,
    /// <summary>
    /// Whether this caregiver's plan includes
    /// <see cref="Domain.Subscriptions.SubscriptionFeature.ProfilePromotion"/>.
    /// </summary>
    bool IsPromoted = false);

/// <summary>
/// MVP ranking order from PLAN.md:
/// service eligibility, verification, distance, availability, rating, experience, price.
///
/// Eligibility and verification are applied as filters before ranking — an ineligible
/// or unverified caregiver is never shown at all, rather than shown last. What remains
/// here is the ordering of caregivers who all already qualify.
///
/// Deliberately a plain comparison rather than a weighted score: a score would need
/// tuning against behavioural data the marketplace does not have yet. Recommendations
/// come later, once there is data to learn from.
/// </summary>
public static class CaregiverRanking
{
    /// <summary>
    /// Which distance band a candidate falls in, for ranking purposes.
    ///
    /// Promotion moves a caregiver up *within* a band rather than across the whole list.
    /// Comparing raw kilometres would make promotion almost meaningless — two candidates
    /// are rarely the same distance to the metre — while ignoring distance entirely
    /// would put a paid caregiver twenty kilometres away above a free one down the road.
    /// </summary>
    private static int DistanceBand(double? distanceKm) =>
        distanceKm is null
            ? int.MaxValue
            : (int)(distanceKm.Value / Subscriptions.EntitlementPolicy.PromotionDistanceBandKm);

    public static IOrderedEnumerable<RankedCaregiver> Apply(IEnumerable<RankedCaregiver> candidates) =>
        candidates
            // Caregivers whose schedule covers the requested window come first. When no
            // window was requested every candidate matches, so this is a no-op.
            .OrderByDescending(c => c.MatchesAvailability)
            // Nearest first, by band. Candidates with no distance sort after those with
            // one, so a missing geocode never masquerades as being close by.
            .ThenBy(c => DistanceBand(c.DistanceKm))
            /*
             * PROFILE_PROMOTION, and deliberately no higher than this.
             *
             * A promoted caregiver is ranked ahead of others who are equally close and
             * equally available — never ahead of eligibility, verification, or someone
             * materially nearer. GoSit is ranking people who will look after a child or
             * a parent, and a subscription is not evidence of suitability. Selling a
             * family a worse match is a different product from the one the plan screen
             * describes.
             *
             * It still sits above rating, which is the uncomfortable part of any paid
             * placement and the reason it is confined to a band rather than applied to
             * the whole list.
             */
            .ThenByDescending(c => c.IsPromoted)
            // Same band-limited nudge as promotion, for the same reason: saying yes to
            // short-notice asks is not evidence of being the better caregiver, so it
            // moves someone up among their equally-close, equally-available peers and no
            // further.
            .ThenByDescending(c => c.Profile.AvailableForLastMinuteRequests)
            // Nearest first within the band, so promotion never costs precision between
            // two caregivers who are not promoted.
            .ThenBy(c => c.DistanceKm ?? double.MaxValue)
            // Unrated caregivers sort below rated ones rather than above: a new caregiver
            // should not outrank a well-reviewed one simply for having no reviews.
            .ThenByDescending(c => c.Profile.AverageRating ?? 0m)
            .ThenByDescending(c => c.Profile.ReviewCount)
            .ThenByDescending(c => c.Profile.YearsOfExperience)
            .ThenBy(c => c.Profile.HourlyRate)
            // Guarantees a stable order across pages when everything above ties.
            .ThenBy(c => c.Profile.Id);

    public static double? DistanceFrom(Address? origin, CaregiverProfile caregiver) =>
        GeoDistance.BetweenKm(origin, caregiver.Address);
}
