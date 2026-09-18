using GoSit.Application.Abstractions;
using GoSit.Domain.Common.ValueObjects;

namespace GoSit.Application.Common;

/// <summary>
/// Attaches coordinates to an address on its way into the domain.
///
/// One place, used by both profile upserts, so caregivers and care requesters can never
/// end up resolved by different rules — which would quietly skew every distance between
/// them.
/// </summary>
public sealed class AddressResolver(IGeocoder geocoder)
{
    /// <summary>
    /// Returns the address with coordinates when they can be found, and unchanged when
    /// they cannot.
    ///
    /// Never throws for a geocoding failure. An unresolved address is still a valid
    /// address — it saves, the profile works, and only distance ranking waits for the
    /// backfill.
    /// </summary>
    public async Task<Address> ResolveAsync(Address address, CancellationToken cancellationToken)
    {
        if (!geocoder.IsLive)
        {
            return address;
        }

        var coordinates = await geocoder.GeocodeAsync(
            new GeocodeRequest(
                address.Line1,
                address.Line2,
                address.City,
                address.Province,
                address.PostalCode,
                address.Country),
            cancellationToken);

        return coordinates is null
            ? address
            : address.WithCoordinates(coordinates.Latitude, coordinates.Longitude);
    }
}
