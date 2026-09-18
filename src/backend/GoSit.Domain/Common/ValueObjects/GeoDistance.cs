namespace GoSit.Domain.Common.ValueObjects;

/// <summary>
/// Great-circle distance between two coordinates.
///
/// Both addresses must be geocoded for this to return a value. Until a geocoding
/// provider is wired up (see IGeocodingService), most stored addresses have no
/// coordinates and search falls back to matching on city.
/// </summary>
public static class GeoDistance
{
    private const double EarthRadiusKm = 6371.0088;

    public static double? BetweenKm(Address? from, Address? to)
    {
        if (from?.Latitude is not { } lat1 || from.Longitude is not { } lon1)
        {
            return null;
        }

        if (to?.Latitude is not { } lat2 || to.Longitude is not { } lon2)
        {
            return null;
        }

        return HaversineKm(lat1, lon1, lat2, lon2);
    }

    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return EarthRadiusKm * 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
