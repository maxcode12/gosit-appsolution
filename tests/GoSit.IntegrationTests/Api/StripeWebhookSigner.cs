using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Builds a genuine <c>Stripe-Signature</c> header for a test payload.
///
/// This mirrors Stripe's documented scheme — <c>t=&lt;timestamp&gt;,v1=&lt;HMAC-SHA256 of
/// "{timestamp}.{payload}"&gt;</c> — so the API verifies these requests with exactly the
/// same code that runs in production. Nothing about verification is stubbed: the tests
/// prove that a wrong secret, a tampered body, and a stale timestamp are all rejected.
/// </summary>
public static class StripeWebhookSigner
{
    /// <summary>Matches the secret configured in <see cref="GoSitApiFactory"/>.</summary>
    public const string TestWebhookSecret = "whsec_integration_test_secret";

    public static string Sign(string payload, DateTime? timestampUtc = null, string? secret = null)
    {
        var timestamp = new DateTimeOffset(timestampUtc ?? DateTime.UtcNow).ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";

        var signature = Convert.ToHexString(
                HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(secret ?? TestWebhookSecret),
                    Encoding.UTF8.GetBytes(signedPayload)))
            .ToLowerInvariant();

        return string.Create(
            CultureInfo.InvariantCulture, $"t={timestamp},v1={signature}");
    }

    /// <summary>A payment_intent.succeeded event body.</summary>
    public static string PaymentSucceeded(string eventId, string paymentIntentId, long amountCents) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "payment_intent.succeeded",
          "data": {
            "object": {
              "id": "{{paymentIntentId}}",
              "object": "payment_intent",
              "amount": {{amountCents}},
              "amount_received": {{amountCents}},
              "currency": "cad",
              "status": "succeeded"
            }
          }
        }
        """;

    public static string PaymentFailed(string eventId, string paymentIntentId, string failureCode) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "payment_intent.payment_failed",
          "data": {
            "object": {
              "id": "{{paymentIntentId}}",
              "object": "payment_intent",
              "amount": 0,
              "currency": "cad",
              "status": "requires_payment_method",
              "last_payment_error": { "code": "{{failureCode}}" }
            }
          }
        }
        """;

    /// <summary>
    /// A transfer.paid event — the only thing that marks a payout paid, the same way
    /// only a webhook marks a payment succeeded.
    /// </summary>
    public static string TransferPaid(string eventId, string transferId, long amountCents) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "transfer.paid",
          "data": {
            "object": {
              "id": "{{transferId}}",
              "object": "transfer",
              "amount": {{amountCents}},
              "currency": "cad"
            }
          }
        }
        """;

    /// <summary>
    /// An account.updated event — how GoSit learns a caregiver may be paid. Sent when
    /// onboarding completes and whenever the account's standing changes afterwards.
    /// </summary>
    public static string AccountUpdated(
        string eventId,
        string accountId,
        bool payoutsEnabled,
        bool detailsSubmitted,
        string? disabledReason = null) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "account.updated",
          "data": {
            "object": {
              "id": "{{accountId}}",
              "object": "account",
              "payouts_enabled": {{(payoutsEnabled ? "true" : "false")}},
              "details_submitted": {{(detailsSubmitted ? "true" : "false")}},
              "requirements": {
                "disabled_reason": {{(disabledReason is null ? "null" : $"\"{disabledReason}\"")}}
              }
            }
          }
        }
        """;

    /// <summary>
    /// A checkout.session.completed event for a subscription — the one that makes a plan
    /// real. The metadata is what GoSit attached when it started checkout.
    /// </summary>
    public static string CheckoutCompleted(
        string eventId,
        string subscriptionId,
        string customerId,
        Guid userId,
        string planCode) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_1",
              "object": "checkout.session",
              "mode": "subscription",
              "status": "complete",
              "customer": "{{customerId}}",
              "subscription": "{{subscriptionId}}",
              "metadata": {
                "gosit_user_id": "{{userId}}",
                "gosit_plan_code": "{{planCode}}"
              }
            }
          }
        }
        """;

    /// <summary>
    /// An invoice.paid event. <c>billing_reason</c> is what separates the first invoice
    /// from a renewal — treating them alike would advance the period twice on signup.
    /// </summary>
    public static string InvoicePaid(
        string eventId,
        string subscriptionId,
        string customerId,
        Guid userId,
        string planCode,
        string billingReason = "subscription_cycle",
        long periodStart = 1_755_000_000,
        long periodEnd = 1_757_678_400) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "invoice.paid",
          "data": {
            "object": {
              "id": "in_test_1",
              "object": "invoice",
              "customer": "{{customerId}}",
              "billing_reason": "{{billingReason}}",
              "period_start": {{periodStart}},
              "period_end": {{periodEnd}},
              "parent": {
                "type": "subscription_details",
                "subscription_details": {
                  "subscription": "{{subscriptionId}}",
                  "metadata": {
                    "gosit_user_id": "{{userId}}",
                    "gosit_plan_code": "{{planCode}}"
                  }
                }
              }
            }
          }
        }
        """;

    public static string InvoicePaymentFailed(
        string eventId,
        string subscriptionId,
        string customerId,
        Guid userId,
        string planCode) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "invoice.payment_failed",
          "data": {
            "object": {
              "id": "in_test_failed",
              "object": "invoice",
              "customer": "{{customerId}}",
              "billing_reason": "subscription_cycle",
              "parent": {
                "type": "subscription_details",
                "subscription_details": {
                  "subscription": "{{subscriptionId}}",
                  "metadata": {
                    "gosit_user_id": "{{userId}}",
                    "gosit_plan_code": "{{planCode}}"
                  }
                }
              }
            }
          }
        }
        """;

    /// <summary>
    /// customer.subscription.deleted — sent when a cancellation takes effect, and when
    /// Stripe gives up on a card it has retried to exhaustion.
    /// </summary>
    public static string SubscriptionDeleted(
        string eventId,
        string subscriptionId,
        string customerId,
        Guid userId,
        string planCode) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "customer.subscription.deleted",
          "data": {
            "object": {
              "id": "{{subscriptionId}}",
              "object": "subscription",
              "status": "canceled",
              "customer": "{{customerId}}",
              "metadata": {
                "gosit_user_id": "{{userId}}",
                "gosit_plan_code": "{{planCode}}"
              }
            }
          }
        }
        """;

    /// <summary>
    /// A charge.refunded event. Stripe reports the running total refunded on the charge,
    /// not the delta, which is what the handler has to reconcile against.
    /// </summary>
    public static string ChargeRefunded(
        string eventId,
        string paymentIntentId,
        long amountRefundedCents,
        string refundId) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "charge.refunded",
          "data": {
            "object": {
              "id": "ch_test_1",
              "object": "charge",
              "payment_intent": "{{paymentIntentId}}",
              "amount": {{amountRefundedCents}},
              "amount_refunded": {{amountRefundedCents}},
              "currency": "cad",
              "refunds": {
                "object": "list",
                "data": [
                  { "id": "{{refundId}}", "object": "refund", "amount": {{amountRefundedCents}} }
                ]
              }
            }
          }
        }
        """;
}
