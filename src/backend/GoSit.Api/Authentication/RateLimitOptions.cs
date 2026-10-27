using System.ComponentModel.DataAnnotations;

namespace GoSit.Api.Authentication;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Requests allowed per window against the credential endpoints, per client IP.</summary>
    [Range(1, 10_000)]
    public int AuthPermitLimit { get; set; } = 10;

    [Range(1, 3600)]
    public int AuthWindowSeconds { get; set; } = 60;

    /// <summary>Requests allowed per window on the provider webhook endpoint, per IP.</summary>
    [Range(1, 100_000)]
    public int WebhookPermitLimit { get; set; } = 300;

    [Range(1, 3600)]
    public int WebhookWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Messages one IP may send through the public contact form per window. Low on purpose:
    /// a person writes to a company once or twice, and the form accepts anonymous input.
    /// </summary>
    [Range(1, 100_000)]
    public int FormPermitLimit { get; set; } = 10;

    [Range(1, 86_400)]
    public int FormWindowSeconds { get; set; } = 3600;

    /// <summary>
    /// Lookups one IP may make against the public "caregivers near you" endpoints per
    /// window. Generous, because typing in a search box fires several requests, but bounded
    /// so the endpoint cannot be used to scrape or to flood the database.
    /// </summary>
    [Range(1, 100_000)]
    public int LookupPermitLimit { get; set; } = 120;

    [Range(1, 3600)]
    public int LookupWindowSeconds { get; set; } = 60;
}
