using System.Globalization;
using System.Reflection;
using GoSit.Application.Abstractions;
using GoSit.Domain.Common.ValueObjects;

namespace GoSit.Infrastructure.Geocoding;

/// <summary>
/// Canadian postal areas loaded once from the data file embedded in this assembly.
///
/// The data is the GeoNames postal-code dataset for Canada (about 1,650 areas), published
/// under Creative Commons Attribution 4.0 — see docs/DEVELOPMENT.md for the attribution
/// the website carries and how to refresh it. One centre point per area, which is the
/// precision proximity needs and no more.
/// </summary>
public sealed class PostalAreaDirectory : IPostalAreaDirectory
{
    private const string ResourceSuffix = "ca-postal-areas.tsv";

    private readonly Dictionary<string, PostalArea> byCode;
    private readonly PostalArea[] all;

    public PostalAreaDirectory()
        : this(ReadEmbeddedData())
    {
    }

    /// <summary>For tests, which supply a handful of rows rather than the whole country.</summary>
    public PostalAreaDirectory(IEnumerable<string> tabSeparatedLines)
    {
        byCode = new Dictionary<string, PostalArea>(StringComparer.Ordinal);

        foreach (var line in tabSeparatedLines)
        {
            var parts = line.Split('\t');

            if (parts.Length < 5
                || !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
                || !double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            {
                continue;
            }

            var code = parts[0].Trim().ToUpperInvariant();
            if (code.Length != 3)
            {
                continue;
            }

            byCode[code] = new PostalArea(code, parts[1].Trim(), parts[2].Trim().ToUpperInvariant(), latitude, longitude);
        }

        all = byCode.Values.OrderBy(a => a.Code, StringComparer.Ordinal).ToArray();
    }

    public int Count => all.Length;

    public PostalArea? Find(string? postalCode)
    {
        var code = NormalizeToArea(postalCode);

        return code is not null && byCode.TryGetValue(code, out var area) ? area : null;
    }

    public IReadOnlyList<PostalArea> Suggest(string? text, int max)
    {
        var typed = text?.Trim();

        if (string.IsNullOrEmpty(typed) || max < 1)
        {
            return [];
        }

        var compact = typed.Replace(" ", string.Empty).ToUpperInvariant();

        // A postal-area prefix ("M5", "M5V", "M5V3L9") matches on the code.
        if (compact.Length <= 6 && char.IsAsciiLetter(compact[0]) && compact.Any(char.IsAsciiDigit))
        {
            var prefix = compact.Length > 3 ? compact[..3] : compact;

            return all.Where(a => a.Code.StartsWith(prefix, StringComparison.Ordinal)).Take(max).ToList();
        }

        // Otherwise it is a place name. Names that start with the text rank above names that
        // merely contain it, so "Toron" finds Toronto before it finds "East Toronto".
        var needle = typed;

        return all
            .Select(a => (Area: a, Index: a.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase)))
            .Where(x => x.Index >= 0)
            .OrderBy(x => x.Index == 0 ? 0 : 1)
            .ThenBy(x => x.Area.Name, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .Select(x => x.Area)
            .ToList();
    }

    public IReadOnlyList<PostalArea> Within(double latitude, double longitude, double radiusKm) =>
        all.Where(a => GeoDistance.HaversineKm(latitude, longitude, a.Latitude, a.Longitude) <= radiusKm).ToList();

    /// <summary>"M5V 3L9" becomes "M5V". Null when it does not start like a Canadian postal code.</summary>
    internal static string? NormalizeToArea(string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return null;
        }

        var compact = postalCode.Replace(" ", string.Empty).ToUpperInvariant();

        if (compact.Length < 3
            || !char.IsAsciiLetter(compact[0])
            || !char.IsAsciiDigit(compact[1])
            || !char.IsAsciiLetter(compact[2]))
        {
            return null;
        }

        return compact[..3];
    }

    private static IEnumerable<string> ReadEmbeddedData()
    {
        var assembly = typeof(PostalAreaDirectory).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("The postal-area data is missing from the build.");
        using var reader = new StreamReader(stream);

        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }
}
