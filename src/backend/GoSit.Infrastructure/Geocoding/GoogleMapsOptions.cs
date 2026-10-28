using System.ComponentModel.DataAnnotations;

namespace GoSit.Infrastructure.Geocoding;

/// <summary>
/// Google Maps Platform (Geocoding API) configuration.
///
/// The API key is a secret: Key Vault in every deployed environment, user secrets
/// locally, never appsettings.json.
/// </summary>
public sealed class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    /// <summary>
    /// Empty selects the next geocoder in line (Azure Maps, then the unconfigured
    /// stand-in), which is what local development and the tests run on.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Restricts results to one country. GoSit is Ontario-first, and without this a
    /// Canadian street name will happily match an American one.
    /// </summary>
    [RegularExpression("^[A-Za-z]{2}$")]
    public string Country { get; set; } = "CA";

    /// <summary>
    /// How long to wait before giving up. Short on purpose — this runs inline while
    /// someone waits for a profile to save.
    /// </summary>
    [Range(1, 30)]
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Overridable so a test can point the real client at a local stub.</summary>
    public string BaseUrl { get; set; } = "https://maps.googleapis.com";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
