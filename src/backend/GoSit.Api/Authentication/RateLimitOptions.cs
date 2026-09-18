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
}
