using System.ComponentModel.DataAnnotations;

namespace GoSit.Infrastructure.Geocoding;

/// <summary>
/// Azure Maps configuration.
///
/// The subscription key is a secret: Key Vault in every deployed environment, user
/// secrets locally, never appsettings.json.
/// </summary>
public sealed class AzureMapsOptions
{
    public const string SectionName = "AzureMaps";

    /// <summary>
    /// Azure Maps subscription key. Empty selects the unconfigured stand-in, which is
    /// what local development and the tests run on.
    /// </summary>
    public string SubscriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Restricts results to one country. GoSit is Ontario-first, and without this a
    /// Canadian street name will happily match an American one.
    /// </summary>
    public string CountrySet { get; set; } = "CA";

    /// <summary>
    /// The lowest match confidence worth storing.
    ///
    /// Azure Maps scores every result, and a low-confidence match is a guess. A
    /// confidently wrong coordinate is worse than none: it puts someone kilometres from
    /// where they are, in every search, invisibly. Below this the result is discarded
    /// and the address stays unresolved.
    /// </summary>
    [Range(0.1, 1.0)]
    public double MinimumConfidence { get; set; } = 0.5;

    /// <summary>
    /// How long to wait before giving up. Short on purpose — this runs inline while
    /// someone waits for a profile to save, and a slow save is worse than a missing
    /// coordinate that a backfill will pick up.
    /// </summary>
    [Range(1, 30)]
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Overridable so a test can point the real client at a local stub.</summary>
    public string BaseUrl { get; set; } = "https://atlas.microsoft.com";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SubscriptionKey);
}
