using GoSit.Domain.Bookings;
using GoSit.Domain.Common.ValueObjects;
using Xunit;

namespace GoSit.UnitTests.Domain;

/// <summary>
/// Comparing a device fix against the booking's address.
///
/// Two properties are worth more than the arithmetic: the coordinates never survive the
/// comparison, and being unable to compare is its own answer rather than a failure.
/// </summary>
public class ArrivalEvidenceTests
{
    // Toronto City Hall.
    private const double AddressLat = 43.6534;
    private const double AddressLon = -79.3841;

    private static Address AddressAt(double? latitude, double? longitude)
    {
        var address = Address.Create(
            "100 Queen St W", null, "Toronto", "ON", "M5H 2N2", "CA");

        return latitude is { } lat && longitude is { } lon
            ? address.WithCoordinates(lat, lon)
            : address;
    }

    [Fact]
    public void Standing_at_the_address_is_verified()
    {
        var evidence = ArrivalEvidence.From(
            AddressLat, AddressLon, AddressAt(AddressLat, AddressLon));

        Assert.Equal(ArrivalVerification.AtTheAddress, evidence.Verification);
        Assert.Equal(0, evidence.DistanceMetres);
    }

    /// <summary>
    /// A city GPS fix is routinely tens of metres out, and the two errors do not cost the
    /// same: a false "away from the address" accuses someone of not turning up when they
    /// did. Sixty metres away is still at the door.
    /// </summary>
    [Fact]
    public void A_normal_gps_error_still_counts_as_being_there()
    {
        // Roughly 60 m north.
        var evidence = ArrivalEvidence.From(
            AddressLat + 0.00054, AddressLon, AddressAt(AddressLat, AddressLon));

        Assert.Equal(ArrivalVerification.AtTheAddress, evidence.Verification);
    }

    [Fact]
    public void Two_streets_away_is_not()
    {
        // Roughly 900 m north — well outside any plausible fix error.
        var evidence = ArrivalEvidence.From(
            AddressLat + 0.0081, AddressLon, AddressAt(AddressLat, AddressLon));

        Assert.Equal(ArrivalVerification.AwayFromTheAddress, evidence.Verification);
        Assert.True(evidence.DistanceMetres > ArrivalEvidence.AcceptedRadiusMetres);
    }

    /// <summary>
    /// Most addresses have no coordinates until geocoding is configured. That is not the
    /// caregiver being somewhere they should not be, and must not be reported as if it
    /// were.
    /// </summary>
    [Fact]
    public void An_ungeocoded_address_is_not_comparable_rather_than_away()
    {
        var evidence = ArrivalEvidence.From(AddressLat, AddressLon, AddressAt(null, null));

        Assert.Equal(ArrivalVerification.NotComparable, evidence.Verification);
        Assert.Null(evidence.DistanceMetres);
    }

    [Fact]
    public void No_fix_at_all_is_not_provided()
    {
        Assert.Equal(ArrivalVerification.NotProvided, ArrivalEvidence.None.Verification);
        Assert.Null(ArrivalEvidence.None.DistanceMetres);
    }

    /// <summary>
    /// The distance is rounded because the precision was never real. A stored number is a
    /// number someone will later read as exact, and "1,247.3 m from the address" in a
    /// dispute claims an accuracy no phone delivered.
    /// </summary>
    [Fact]
    public void The_distance_is_rounded_to_the_nearest_ten_metres()
    {
        var evidence = ArrivalEvidence.From(
            AddressLat + 0.0081, AddressLon, AddressAt(AddressLat, AddressLon));

        Assert.NotNull(evidence.DistanceMetres);
        Assert.Equal(0, evidence.DistanceMetres!.Value % 10);
    }
}
