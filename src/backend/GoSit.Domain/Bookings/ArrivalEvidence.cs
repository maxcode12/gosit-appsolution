using GoSit.Domain.Common.ValueObjects;

namespace GoSit.Domain.Bookings;

/// <summary>
/// Whether a check-in or check-out happened where the booking said it would.
/// </summary>
public enum ArrivalVerification
{
    /// <summary>
    /// No location was offered — the device could not fix one, or permission was
    /// refused. Deliberately not a failure: see <see cref="ArrivalEvidence"/>.
    /// </summary>
    NotProvided = 1,

    /// <summary>
    /// A location was offered but the booking's address has no coordinates, so there was
    /// nothing to compare it against. Common until every address is geocoded.
    /// </summary>
    NotComparable = 2,

    /// <summary>Within the accepted radius of the address.</summary>
    AtTheAddress = 3,

    /// <summary>Outside it. Recorded, never refused.</summary>
    AwayFromTheAddress = 4,
}

/// <summary>
/// What is kept about where a caregiver was when they started or finished.
///
/// Two decisions are encoded here, and both are the point of the type.
///
/// **It records a distance, not a place.** The device's coordinates are compared against
/// the booking's address and then discarded; what survives is how far apart they were.
/// That is the whole of what a dispute needs — "they were at the home" or "they were two
/// kilometres away" — and it is strictly less revealing than keeping a fix on a person.
/// Storing the raw point would build a location history of caregivers as a side effect
/// of a feature nobody asked for that from.
///
/// **It never refuses.** Location fixes fail indoors, in basements, on old handsets, and
/// whenever someone declines the permission. A caregiver standing at the door unable to
/// start work because the GPS will not settle is a worse outcome than an unverified
/// check-in, and it would fall hardest on the people with the worst phones. So the
/// evidence is recorded and shown; it is not a gate.
/// </summary>
public sealed record ArrivalEvidence(ArrivalVerification Verification, double? DistanceMetres)
{
    /// <summary>
    /// How far from the address still counts as being there.
    ///
    /// Generous on purpose. A consumer GPS fix in a city is routinely tens of metres out
    /// and worse between tall buildings, and the cost of the two errors is not
    /// symmetrical: a false "away from the address" accuses someone of not turning up
    /// when they did. A wide radius makes that rare, and someone two streets away is
    /// still comfortably outside it.
    /// </summary>
    public const double AcceptedRadiusMetres = 250;

    public static readonly ArrivalEvidence None = new(ArrivalVerification.NotProvided, null);

    /// <summary>
    /// Compares a device fix against the booking's address. The coordinates go no
    /// further than this method.
    /// </summary>
    public static ArrivalEvidence From(double latitude, double longitude, Address? address)
    {
        if (address?.Latitude is not { } addressLat || address.Longitude is not { } addressLon)
        {
            return new ArrivalEvidence(ArrivalVerification.NotComparable, null);
        }

        var metres = GeoDistance.HaversineKm(latitude, longitude, addressLat, addressLon) * 1000;

        // Rounded to the nearest ten metres. The extra precision is spurious next to the
        // accuracy of the fix, and a stored number is a number someone will later read
        // as exact.
        var rounded = Math.Round(metres / 10) * 10;

        return new ArrivalEvidence(
            rounded <= AcceptedRadiusMetres
                ? ArrivalVerification.AtTheAddress
                : ArrivalVerification.AwayFromTheAddress,
            rounded);
    }
}
