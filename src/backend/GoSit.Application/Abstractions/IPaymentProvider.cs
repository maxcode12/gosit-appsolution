namespace GoSit.Application.Abstractions;

/// <summary>
/// What GoSit asks a payment provider to do. Stripe is the first implementation; the
/// abstraction exists so the domain and handlers never reference Stripe types directly
/// (see docs/ARCHITECTURE.md).
///
/// Amounts crossing this boundary are decimal. Converting to the provider's integer
/// minor units is the implementation's job.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// Creates, or returns, the intent to collect an amount for a booking.
    ///
    /// <paramref name="idempotencyKey"/> is derived from the booking, so a retried
    /// request reuses the existing intent instead of creating a second charge.
    /// </summary>
    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken);

    Task<ProviderRefundResult> RefundAsync(
        string providerPaymentIntentId,
        decimal amount,
        string? reason,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sends a caregiver's earnings. Requires the caregiver to have completed Connect
    /// onboarding; see the payout gap recorded in docs/PLAN.md.
    /// </summary>
    Task<ProviderTransferResult> CreateTransferAsync(
        string destinationAccountId,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

/// <summary>
/// The client secret is what the browser uses to confirm payment. It is scoped to this
/// one intent, is returned only to the requester who owns the booking, and is never
/// logged.
/// </summary>
public sealed record PaymentIntentResult(string ProviderPaymentIntentId, string ClientSecret);

public sealed record ProviderRefundResult(string ProviderRefundId, decimal Amount);

public sealed record ProviderTransferResult(string ProviderTransferId);

/// <summary>
/// Verifies and parses provider webhooks into a shape the application understands.
///
/// Verification is the security boundary for the whole payment flow: without it anyone
/// who knows the URL could declare a booking paid.
/// </summary>
public interface IPaymentWebhookParser
{
    /// <summary>
    /// Throws <see cref="WebhookVerificationException"/> when the signature is missing,
    /// wrong, or too old to be a fresh delivery.
    /// </summary>
    PaymentWebhookEvent Parse(string payload, string? signatureHeader);
}

public sealed class WebhookVerificationException(string message) : Exception(message);

public enum PaymentWebhookEventType
{
    /// <summary>Anything GoSit does not act on. Acknowledged and ignored.</summary>
    Unhandled = 0,
    PaymentSucceeded = 1,
    PaymentFailed = 2,
    RefundCompleted = 3,
    PayoutPaid = 4,
    PayoutFailed = 5,

    /// <summary>
    /// Checkout completed and the provider has started billing. This — not the browser
    /// returning to a success URL — is what makes a subscription real.
    /// </summary>
    SubscriptionActivated = 6,

    /// <summary>A new billing period was paid for.</summary>
    SubscriptionRenewed = 7,

    /// <summary>An invoice failed. Entitlements continue through the grace period.</summary>
    SubscriptionPaymentFailed = 8,

    /// <summary>The provider ended the subscription, by cancellation or by giving up.</summary>
    SubscriptionCancelled = 9,

    /// <summary>
    /// A caregiver's connected account changed — onboarding finished, or the provider
    /// enabled or restricted payouts. This is how GoSit learns it may send money.
    /// </summary>
    PayoutAccountUpdated = 10,
}

/// <summary>
/// A verified provider event, reduced to what GoSit acts on.
///
/// <see cref="EventId"/> is the provider's own id and is the deduplication key — webhooks
/// are delivered at least once and can arrive twice.
/// </summary>
public sealed record PaymentWebhookEvent(
    string EventId,
    PaymentWebhookEventType Type,
    string? ProviderPaymentIntentId,
    decimal? Amount,
    string? ProviderRefundId,
    string? ProviderTransferId,
    string? FailureCode,
    /// <summary>Present only on the subscription event types.</summary>
    SubscriptionWebhookDetails? Subscription = null,
    /// <summary>Present only on <see cref="PaymentWebhookEventType.PayoutAccountUpdated"/>.</summary>
    PayoutAccountWebhookDetails? PayoutAccount = null);

/// <summary>
/// A connected account's current state, as the provider reports it.
///
/// Matched to a caregiver by the account id we stored when we created it, not by
/// metadata — the account reference is already ours and is the more direct key.
/// </summary>
public sealed record PayoutAccountWebhookDetails(
    string AccountId,
    bool PayoutsEnabled,
    bool DetailsSubmitted,
    string? DisabledReason);

/// <summary>
/// The subscription-shaped part of a webhook.
///
/// <see cref="UserId"/> and <see cref="PlanCode"/> come from metadata GoSit attached
/// when it started checkout, so an event is matched to an account by something we set
/// rather than by anything the browser reported.
///
/// The period is the provider's, and it is authoritative — see
/// <c>UserSubscription.SyncBillingPeriod</c>.
/// </summary>
public sealed record SubscriptionWebhookDetails(
    string? ProviderSubscriptionId,
    string? ProviderCustomerId,
    Guid? UserId,
    string? PlanCode,
    DateTime? CurrentPeriodStartUtc,
    DateTime? CurrentPeriodEndUtc);
