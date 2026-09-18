namespace GoSit.Application.Abstractions;

/// <summary>
/// What GoSit asks a billing provider to do for recurring subscriptions.
///
/// Separate from <see cref="IPaymentProvider"/> on purpose. A booking payment is a
/// one-off charge GoSit initiates and reconciles; a subscription is a relationship the
/// provider owns and drives — it decides when to bill, when to retry, and when to give
/// up. Folding both into one interface would have hidden that difference.
///
/// As with payments, no Stripe type crosses this boundary.
/// </summary>
public interface ISubscriptionBillingProvider
{
    /// <summary>
    /// Whether real billing is configured.
    ///
    /// The UI surfaces this rather than guessing: with no credentials the simulated
    /// implementation stands in, plans grant entitlements without payment, and telling
    /// someone they are being charged when they are not would be a lie.
    /// </summary>
    bool IsLive { get; }

    /// <summary>
    /// Returns the provider customer for this account, creating one if needed.
    /// Idempotent on <paramref name="idempotencyKey"/>, so a retry cannot leave an
    /// account with two customers and a split billing history.
    /// </summary>
    Task<string> EnsureCustomerAsync(
        string? existingCustomerId,
        string email,
        string fullName,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken);

    /// <summary>
    /// Starts a hosted checkout for a plan and returns where to send the browser.
    ///
    /// Hosted checkout rather than collecting card details ourselves: it keeps card data
    /// entirely off GoSit's infrastructure, and it handles 3-D Secure, wallets and
    /// per-market payment methods that a hand-rolled form would not.
    ///
    /// The subscription is NOT live when this returns. It becomes live when the provider
    /// says so, over a verified webhook.
    /// </summary>
    Task<BillingCheckoutSession> StartCheckoutAsync(
        BillingCheckoutRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stops the renewal at the provider, leaving the paid period intact. Mirrors
    /// <c>UserSubscription.CancelAtEndOfPeriod</c>, which is what someone who has paid
    /// for a month reasonably expects.
    /// </summary>
    Task CancelAtPeriodEndAsync(
        string providerSubscriptionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// A link to the provider's own billing portal, where someone can change their card,
    /// read invoices and cancel. Building those screens ourselves would mean handling
    /// card data and reimplementing tax receipts for no benefit.
    /// </summary>
    Task<string> CreateBillingPortalUrlAsync(
        string providerCustomerId,
        string returnUrl,
        CancellationToken cancellationToken);
}

public sealed record BillingCheckoutRequest(
    string CustomerId,
    string PriceId,
    string SuccessUrl,
    string CancelUrl,
    /// <summary>Derived from the account and plan, so a double submit reuses the session.</summary>
    string IdempotencyKey,
    /// <summary>
    /// Travels with the provider's objects and comes back on every webhook. This is how
    /// an event is matched to an account without trusting anything the browser sends.
    /// </summary>
    IReadOnlyDictionary<string, string> Metadata);

public sealed record BillingCheckoutSession(string SessionId, string Url);

/// <summary>
/// The metadata keys GoSit attaches to provider objects.
///
/// Shared between the provider that writes them and the parser that reads them, so a
/// rename cannot silently orphan every future webhook — which would be invisible until
/// someone's renewal failed to apply months later.
/// </summary>
public static class SubscriptionMetadataKeys
{
    public const string UserId = "gosit_user_id";
    public const string PlanCode = "gosit_plan_code";
}
