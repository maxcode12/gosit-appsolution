using System.ComponentModel.DataAnnotations;

namespace GoSit.Infrastructure.Payments;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>
    /// Stripe secret key. Comes from Azure Key Vault in every deployed environment and
    /// must never appear in appsettings.json or a log line.
    ///
    /// Empty locally, which selects the simulated provider instead — see
    /// <see cref="SimulatedPaymentProvider"/>.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Endpoint signing secret used to verify webhook signatures. Also from Key Vault.
    /// Without it, webhook verification cannot run and the endpoint refuses everything.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// How much clock difference to tolerate on a webhook timestamp, guarding against
    /// replay. Stripe's own libraries default to 5 minutes; never set this to 0, which
    /// disables the recency check entirely.
    /// </summary>
    [Range(30, 900)]
    public int WebhookToleranceSeconds { get; set; } = 300;

    /// <summary>
    /// Overrides where Stripe's API lives.
    ///
    /// Exists so tests can point the real provider at a local stub and assert on the
    /// requests it actually sends — the SDK's own serialization of our options is the
    /// part unit tests cannot reach, and getting an option name wrong is exactly the
    /// mistake that only shows up in production.
    ///
    /// Never set outside tests. Empty means Stripe's real API.
    /// </summary>
    public string ApiBase { get; set; } = string.Empty;

    /// <summary>True when real Stripe credentials are configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);
}
