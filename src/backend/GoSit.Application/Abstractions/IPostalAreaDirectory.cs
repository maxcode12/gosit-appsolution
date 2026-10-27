namespace GoSit.Application.Abstractions;

/// <summary>
/// One Canadian postal area: the first three characters of a postal code (its forward
/// sortation area, "M5V") with a name and a centre point.
/// </summary>
public sealed record PostalArea(string Code, string Name, string Province, double Latitude, double Longitude);

/// <summary>
/// Looks up Canadian postal areas from data held inside the application.
///
/// Held locally rather than fetched from a mapping service for three reasons: it costs
/// nothing, it cannot be rate-limited or switched off by a provider, and a postal area
/// is the right precision for the job. "How many caregivers are near M5V" does not need a
/// street address, and a street address is exactly what must not be needed to browse.
/// </summary>
public interface IPostalAreaDirectory
{
    /// <summary>
    /// Finds the area for a postal code in any common form ("M5V 3L9", "m5v3l9", "M5V"),
    /// or null when it is not a Canadian postal area.
    /// </summary>
    PostalArea? Find(string? postalCode);

    /// <summary>
    /// Areas matching typed text, for a search box: by postal-area prefix ("M5") or by
    /// place name ("toron"). Best matches first, at most <paramref name="max"/>.
    /// </summary>
    IReadOnlyList<PostalArea> Suggest(string? text, int max);

    /// <summary>Every area whose centre lies within <paramref name="radiusKm"/> of a point.</summary>
    IReadOnlyList<PostalArea> Within(double latitude, double longitude, double radiusKm);
}
