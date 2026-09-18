namespace GoSit.Domain.Common.ValueObjects;

/// <summary>
/// A Canadian postal address.
///
/// The full address is sensitive: an exact home address must never be exposed before
/// a booking authorises the disclosure. Use <see cref="ToApproximate"/> for anything
/// shown to a user who is not yet entitled to the precise location.
/// </summary>
public sealed class Address
{
    private Address()
    {
    }

    public string Line1 { get; private set; } = string.Empty;

    public string? Line2 { get; private set; }

    public string City { get; private set; } = string.Empty;

    /// <summary>Two-letter province or territory code, e.g. "ON".</summary>
    public string Province { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    public string Country { get; private set; } = "CA";

    /// <summary>Set by geocoding. Null until the address has been resolved.</summary>
    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public static Address Create(
        string line1,
        string? line2,
        string city,
        string province,
        string postalCode,
        string country = "CA")
    {
        if (string.IsNullOrWhiteSpace(line1))
        {
            throw new DomainException("A street address is required.");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new DomainException("A city is required.");
        }

        if (string.IsNullOrWhiteSpace(province) || province.Trim().Length != 2)
        {
            throw new DomainException("A two-letter province code is required.");
        }

        if (!IsValidCanadianPostalCode(postalCode))
        {
            throw new DomainException("A valid Canadian postal code is required.");
        }

        return new Address
        {
            Line1 = line1.Trim(),
            Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2.Trim(),
            City = city.Trim(),
            Province = province.Trim().ToUpperInvariant(),
            PostalCode = NormalizePostalCode(postalCode),
            Country = country.Trim().ToUpperInvariant(),
        };
    }

    public Address WithCoordinates(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new DomainException("Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new DomainException("Longitude must be between -180 and 180.");
        }

        return new Address
        {
            Line1 = Line1,
            Line2 = Line2,
            City = City,
            Province = Province,
            PostalCode = PostalCode,
            Country = Country,
            Latitude = latitude,
            Longitude = longitude,
        };
    }

    /// <summary>
    /// City, province, and the postal code's forward sortation area only — enough to
    /// judge distance without revealing where someone lives.
    /// </summary>
    public string ToApproximate() =>
        $"{City}, {Province} {PostalCode[..Math.Min(3, PostalCode.Length)]}";

    /// <summary>Stored without the inner space: "M5V3L9".</summary>
    public static string NormalizePostalCode(string postalCode) =>
        postalCode.Replace(" ", string.Empty).ToUpperInvariant();

    private static bool IsValidCanadianPostalCode(string postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return false;
        }

        var normalized = NormalizePostalCode(postalCode);

        // ANA NAN, where A is a letter and N a digit.
        return normalized.Length == 6
            && char.IsAsciiLetter(normalized[0])
            && char.IsAsciiDigit(normalized[1])
            && char.IsAsciiLetter(normalized[2])
            && char.IsAsciiDigit(normalized[3])
            && char.IsAsciiLetter(normalized[4])
            && char.IsAsciiDigit(normalized[5]);
    }
}
