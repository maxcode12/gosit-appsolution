using GoSit.Application.Abstractions;
using GoSit.Domain.Payments;
using Microsoft.Extensions.Options;
using Stripe;

namespace GoSit.Infrastructure.Payments;

/// <summary>
/// Verifies and parses Stripe webhooks.
///
/// This is the security boundary for the entire payment flow. Without signature
/// verification, anyone who learned the endpoint URL could declare bookings paid.
/// Verification uses Stripe's own <c>EventUtility.ConstructEvent</c>, which checks the
/// HMAC in the <c>Stripe-Signature</c> header and rejects timestamps outside the
/// configured tolerance, so a captured payload cannot be replayed later.
/// </summary>
public sealed class StripeWebhookParser(IOptions<StripeOptions> options) : IPaymentWebhookParser
{
    private readonly StripeOptions _options = options.Value;

    public PaymentWebhookEvent Parse(string payload, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            throw new WebhookVerificationException("The webhook signature header is missing.");
        }

        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            // Refusing is the only safe behaviour: without a secret nothing can be
            // verified, and accepting unverified payment events would be far worse
            // than rejecting real ones.
            throw new WebhookVerificationException("Webhook verification is not configured.");
        }

        Event stripeEvent;

        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                signatureHeader,
                _options.WebhookSecret,
                _options.WebhookToleranceSeconds,
                // Tolerate Stripe having moved to a newer API version than the SDK
                // targets; a version bump must not stop payments being recorded.
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            throw new WebhookVerificationException(
                $"The webhook signature could not be verified: {ex.Message}");
        }

        return Translate(stripeEvent);
    }

    private static PaymentWebhookEvent Translate(Event stripeEvent) => stripeEvent.Type switch
    {
        "payment_intent.succeeded" when stripeEvent.Data.Object is PaymentIntent intent =>
            new PaymentWebhookEvent(
                stripeEvent.Id,
                PaymentWebhookEventType.PaymentSucceeded,
                intent.Id,
                // What Stripe actually collected, which the aggregate checks against
                // what GoSit expected.
                Money.FromMinorUnits(intent.AmountReceived),
                null,
                null,
                null),

        "payment_intent.payment_failed" when stripeEvent.Data.Object is PaymentIntent intent =>
            new PaymentWebhookEvent(
                stripeEvent.Id,
                PaymentWebhookEventType.PaymentFailed,
                intent.Id,
                null,
                null,
                null,
                intent.LastPaymentError?.Code),

        "charge.refunded" when stripeEvent.Data.Object is Charge charge =>
            new PaymentWebhookEvent(
                stripeEvent.Id,
                PaymentWebhookEventType.RefundCompleted,
                charge.PaymentIntentId,
                Money.FromMinorUnits(charge.AmountRefunded),
                charge.Refunds?.Data?.LastOrDefault()?.Id,
                null,
                null),

        "transfer.paid" when stripeEvent.Data.Object is Transfer transfer =>
            new PaymentWebhookEvent(
                stripeEvent.Id,
                PaymentWebhookEventType.PayoutPaid,
                null,
                Money.FromMinorUnits(transfer.Amount),
                null,
                transfer.Id,
                null),

        "transfer.failed" when stripeEvent.Data.Object is Transfer transfer =>
            new PaymentWebhookEvent(
                stripeEvent.Id,
                PaymentWebhookEventType.PayoutFailed,
                null,
                null,
                null,
                transfer.Id,
                null),

        // ---- Subscriptions ------------------------------------------------------
        //
        // checkout.session.completed is what makes a subscription real. The browser
        // returning to a success URL proves nothing — it can be typed by hand.
        "checkout.session.completed" when stripeEvent.Data.Object is Stripe.Checkout.Session session
            && session.Mode == "subscription" =>
            Subscription(
                stripeEvent,
                PaymentWebhookEventType.SubscriptionActivated,
                session.SubscriptionId,
                session.CustomerId,
                session.Metadata,
                null,
                null),

        // Stripe distinguishes the first invoice from a recurring one by billing_reason.
        // Treating them alike would advance the period twice on the very first payment.
        "invoice.paid" when stripeEvent.Data.Object is Invoice invoice =>
            Subscription(
                stripeEvent,
                invoice.BillingReason == "subscription_cycle"
                    ? PaymentWebhookEventType.SubscriptionRenewed
                    : PaymentWebhookEventType.SubscriptionActivated,
                SubscriptionIdOf(invoice),
                invoice.CustomerId,
                invoice.Parent?.SubscriptionDetails?.Metadata ?? invoice.Metadata,
                invoice.PeriodStart,
                invoice.PeriodEnd),

        "invoice.payment_failed" when stripeEvent.Data.Object is Invoice invoice =>
            Subscription(
                stripeEvent,
                PaymentWebhookEventType.SubscriptionPaymentFailed,
                SubscriptionIdOf(invoice),
                invoice.CustomerId,
                invoice.Parent?.SubscriptionDetails?.Metadata ?? invoice.Metadata,
                null,
                null),

        // Sent when a cancellation takes effect, and when Stripe gives up on a card it
        // has retried to exhaustion.
        "customer.subscription.deleted" when stripeEvent.Data.Object is Subscription subscription =>
            Subscription(
                stripeEvent,
                PaymentWebhookEventType.SubscriptionCancelled,
                subscription.Id,
                subscription.CustomerId,
                subscription.Metadata,
                null,
                null),

        // ---- Connect accounts ---------------------------------------------------
        //
        // How GoSit learns a caregiver may be paid. Sent when onboarding completes and
        // whenever Stripe changes the account's standing afterwards — an expired
        // document can restrict an account that was working yesterday.
        "account.updated" when stripeEvent.Data.Object is Account account =>
            new PaymentWebhookEvent(
                stripeEvent.Id,
                PaymentWebhookEventType.PayoutAccountUpdated,
                null,
                null,
                null,
                null,
                null,
                null,
                new PayoutAccountWebhookDetails(
                    account.Id,
                    account.PayoutsEnabled,
                    account.DetailsSubmitted,
                    account.Requirements?.DisabledReason)),

        // Acknowledged and ignored. Stripe advises subscribing only to the events an
        // integration needs, but an endpoint still has to cope with anything arriving.
        _ => new PaymentWebhookEvent(
            stripeEvent.Id, PaymentWebhookEventType.Unhandled, null, null, null, null, null),
    };

    /// <summary>
    /// Builds a subscription event, reading the account and plan out of the metadata Go
    /// Sit attached when it started checkout.
    ///
    /// A metadata user id that is missing or unparseable yields null rather than an
    /// exception: the handler then ignores the event instead of the whole endpoint
    /// erroring, which would make Stripe retry a payload that can never succeed.
    /// </summary>
    private static PaymentWebhookEvent Subscription(
        Event stripeEvent,
        PaymentWebhookEventType type,
        string? providerSubscriptionId,
        string? providerCustomerId,
        IDictionary<string, string>? metadata,
        DateTime? periodStartUtc,
        DateTime? periodEndUtc)
    {
        Guid? userId = null;

        if (metadata is not null
            && metadata.TryGetValue(SubscriptionMetadataKeys.UserId, out var raw)
            && Guid.TryParse(raw, out var parsed))
        {
            userId = parsed;
        }

        string? planCode = null;
        metadata?.TryGetValue(SubscriptionMetadataKeys.PlanCode, out planCode);

        return new PaymentWebhookEvent(
            stripeEvent.Id,
            type,
            null,
            null,
            null,
            null,
            null,
            new SubscriptionWebhookDetails(
                providerSubscriptionId,
                providerCustomerId,
                userId,
                planCode,
                periodStartUtc,
                periodEndUtc));
    }

    /// <summary>
    /// The subscription an invoice belongs to.
    ///
    /// Stripe moved this from a top-level <c>subscription</c> field onto the invoice's
    /// line items in recent API versions, so both are checked — an integration that read
    /// only one would break silently on an API version bump.
    /// </summary>
    private static string? SubscriptionIdOf(Invoice invoice) =>
        invoice.Parent?.SubscriptionDetails?.SubscriptionId
        ?? invoice.Lines?.Data?
            .Select(line => line.Parent?.SubscriptionItemDetails?.Subscription)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
}
