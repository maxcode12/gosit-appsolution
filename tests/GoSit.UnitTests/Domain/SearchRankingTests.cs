using GoSit.Application.Features.Search.SearchCaregivers;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Common.ValueObjects;
using Xunit;

namespace GoSit.UnitTests.Domain;

public class SearchRankingTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    private static CaregiverProfile Caregiver(
        decimal rate = 25m,
        int experience = 3,
        decimal? rating = null,
        int reviews = 0)
    {
        var profile = CaregiverProfile.Create(Guid.NewGuid(), rate, experience, Now);

        if (rating is not null || reviews > 0)
        {
            profile.UpdateRatingSummary(rating, reviews, Now);
        }

        return profile;
    }

    private static List<CaregiverProfile> Rank(params RankedCaregiver[] candidates) =>
        CaregiverRanking.Apply(candidates).Select(c => c.Profile).ToList();

    [Fact]
    public void Caregivers_matching_the_requested_window_rank_above_those_that_do_not()
    {
        var available = Caregiver(rating: 3m, reviews: 5);
        var unavailable = Caregiver(rating: 5m, reviews: 50);

        var ranked = Rank(
            new RankedCaregiver(unavailable, MatchesAvailability: false, DistanceKm: 1),
            new RankedCaregiver(available, MatchesAvailability: true, DistanceKm: 40));

        // Availability wins over both rating and distance: a caregiver who cannot work
        // the requested hours is not a useful result however good they look.
        Assert.Equal(available, ranked[0]);
    }

    [Fact]
    public void Nearer_caregivers_rank_first()
    {
        var near = Caregiver();
        var far = Caregiver();

        var ranked = Rank(
            new RankedCaregiver(far, true, 30),
            new RankedCaregiver(near, true, 2));

        Assert.Equal(near, ranked[0]);
    }

    /// <summary>
    /// A caregiver whose distance is unknown must not be treated as nearby.
    /// </summary>
    [Fact]
    public void Caregivers_with_no_known_distance_rank_after_those_with_one()
    {
        var known = Caregiver();
        var unknown = Caregiver();

        var ranked = Rank(
            new RankedCaregiver(unknown, true, null),
            new RankedCaregiver(known, true, 95));

        Assert.Equal(known, ranked[0]);
    }

    [Fact]
    public void Better_rated_caregivers_rank_first_at_the_same_distance()
    {
        var betterRated = Caregiver(rating: 4.8m, reviews: 20);
        var worseRated = Caregiver(rating: 3.2m, reviews: 20);

        var ranked = Rank(
            new RankedCaregiver(worseRated, true, 5),
            new RankedCaregiver(betterRated, true, 5));

        Assert.Equal(betterRated, ranked[0]);
    }

    /// <summary>
    /// A caregiver with no reviews should not outrank a well-reviewed one just for
    /// having nothing on record.
    /// </summary>
    [Fact]
    public void An_unrated_caregiver_ranks_below_a_rated_one()
    {
        var rated = Caregiver(rating: 4m, reviews: 10);
        var unrated = Caregiver();

        var ranked = Rank(
            new RankedCaregiver(unrated, true, 5),
            new RankedCaregiver(rated, true, 5));

        Assert.Equal(rated, ranked[0]);
    }

    [Fact]
    public void Experience_breaks_a_tie_on_rating()
    {
        var experienced = Caregiver(experience: 10, rating: 4m, reviews: 5);
        var newer = Caregiver(experience: 1, rating: 4m, reviews: 5);

        var ranked = Rank(
            new RankedCaregiver(newer, true, 5),
            new RankedCaregiver(experienced, true, 5));

        Assert.Equal(experienced, ranked[0]);
    }

    [Fact]
    public void The_cheaper_caregiver_wins_when_everything_else_ties()
    {
        var cheaper = Caregiver(rate: 20m, experience: 5, rating: 4m, reviews: 5);
        var pricier = Caregiver(rate: 45m, experience: 5, rating: 4m, reviews: 5);

        var ranked = Rank(
            new RankedCaregiver(pricier, true, 5),
            new RankedCaregiver(cheaper, true, 5));

        Assert.Equal(cheaper, ranked[0]);
    }

    /// <summary>
    /// Without a final tiebreak the same caregiver could appear on two pages, or on none.
    /// </summary>
    [Fact]
    public void Ranking_is_stable_for_identical_caregivers()
    {
        var a = Caregiver();
        var b = Caregiver();

        var first = Rank(new RankedCaregiver(a, true, 5), new RankedCaregiver(b, true, 5));
        var second = Rank(new RankedCaregiver(b, true, 5), new RankedCaregiver(a, true, 5));

        Assert.Equal(first.Select(c => c.Id), second.Select(c => c.Id));
    }

    [Fact]
    public void Distance_is_null_when_either_address_lacks_coordinates()
    {
        var withCoordinates = Address
            .Create("12 Bloor St W", null, "Toronto", "ON", "M5S 1M2")
            .WithCoordinates(43.6677, -79.3948);
        var withoutCoordinates = Address.Create("88 Queen St E", null, "Toronto", "ON", "M5C 1S6");

        Assert.Null(GeoDistance.BetweenKm(withCoordinates, withoutCoordinates));
        Assert.Null(GeoDistance.BetweenKm(null, withCoordinates));
    }

    [Fact]
    public void Distance_between_two_known_points_is_measured_in_kilometres()
    {
        // Toronto City Hall to the CN Tower is roughly 1.3 km.
        var distance = GeoDistance.HaversineKm(43.6534, -79.3841, 43.6426, -79.3871);

        Assert.InRange(distance, 1.0, 1.6);
    }

    [Fact]
    public void An_address_without_coordinates_yields_no_distance()
    {
        var address = Address.Create("12 Bloor St W", null, "Toronto", "ON", "M5S 1M2");

        Assert.Null(address.Latitude);
        Assert.Null(GeoDistance.BetweenKm(address, address));
    }
}
