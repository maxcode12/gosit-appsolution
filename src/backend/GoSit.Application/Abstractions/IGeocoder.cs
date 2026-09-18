namespace GoSit.Application.Abstractions;

/// <summary>
/// Turns a postal address into coordinates.
///
/// Distance ranking is the difference between "here are caregivers" and "here are
/// caregivers near you", and it cannot work until addresses are resolved. Everything
/// downstream — the radius filter, the ordering, the "2 km away" on a card — depends on
/// this one call.
///
/// <b>A failure here is never fatal.</b> An address that could not be resolved is still
/// a valid address: it saves, the profile works, and only distance ranking is missing
/// until a backfill catches it. Refusing to save someone's address because a mapping
/// service was briefly unreachable would be the wrong trade.
/// </summary>
public interface IGeocoder
{
    /// <summary>
    /// Whether a real provider is configured. With none, callers skip the attempt
    /// rather than logging a failure on every save.
    /// </summary>
    bool IsLive { get; }

    /// <summary>
    /// Resolves an address, or returns null when it cannot be resolved confidently.
    ///
    /// Null covers both "the provider could not find it" and "the provider found
    /// something it was not sure about" — a confidently wrong coordinate is worse than
    /// none, because it silently puts someone in the wrong place in every search.
    /// </summary>
    Task<GeoCoordinates?> GeocodeAsync(GeocodeRequest address, CancellationToken cancellationToken);
}

/// <summary>
/// An address to resolve.
///
/// Its own type rather than the API's <c>AddressDto</c>, so the abstraction does not
/// depend on a feature's contract and a change to one cannot silently reshape the other.
/// </summary>
public sealed record GeocodeRequest(
    string Line1,
    string? Line2,
    string City,
    string Province,
    string PostalCode,
    string Country);

public sealed record GeoCoordinates(double Latitude, double Longitude);
