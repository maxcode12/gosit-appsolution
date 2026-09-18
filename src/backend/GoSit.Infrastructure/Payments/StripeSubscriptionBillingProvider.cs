using GoSit.Application.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace GoSit.Infrastructure.Payments;

/// <summary>
/// Recurring billing through Stripe Checkout and Stripe Billing.
///
/// The shape of the integration, and why:
///
/// - **Hosted Checkout, not a card form.** Card details never touch GoSit's servers, and
///   Stripe handles 3-D Secure, wallets and per-market payment methods. Collecting cards
///   ourselves would buy nothing and cost a PCI scope.
/// - **Stripe owns the schedule.** It decides when to bill, when to retry a failed card,
///   and when to give up. GoSit reacts to what it reports rather than running its own
///   renewal clock.
/// - **Metadata carries the account.** Every object gets the GoSit user id and plan
///   code, so a webhook is matched to an account by something we set — never by
///   anything the browser told us.
/// - **Idempotency keys on every write**, so a double-submitted form or a retried
///   request cannot produce two customers or two checkout sessions.
/// </summary>
public sealed class StripeSubscriptionBillingProvider : ISubscriptionBillingProvider
{
    private readonly CustomerService _customers;
    private readonly SessionService _sessions;
    private readonly SubscriptionService _subscriptions;
    private readonly Stripe.BillingPortal.SessionService _portalSessions;

    public StripeSubscriptionBillingProvider(IOptions<StripeOptions> options)
    {
        var apiBase = options.Value.ApiBase;

        var client = string.IsNullOrWhiteSpace(apiBase)
            ? new StripeClient(options.Value.SecretKey)
            // Test-only. See StripeOptions.ApiBase.
            : new StripeClient(options.Value.SecretKey, apiBase: apiBase);

        _customers = new CustomerService(client);
        _sessions = new SessionService(client);
        _subscriptions = new SubscriptionService(client);
        _portalSessions = new Stripe.BillingPortal.SessionService(client);
    }

    public bool IsLive => true;

    public async Task<string> EnsureCustomerAsync(
        string? existingCustomerId,
        string email,
        string fullName,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(existingCustomerId))
        {
            return existingCustomerId;
        }

        var customer = await _customers.CreateAsync(
            new CustomerCreateOptions
            {
                Email = email,
                Name = fullName,
                Metadata = new Dictionary<string, string>(metadata),
            },
            new RequestOptions { IdempotencyKey = idempotencyKey },
            cancellationToken);

        return customer.Id;
    }

    public async Task<BillingCheckoutSession> StartCheckoutAsync(
        BillingCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>(request.Metadata);

        var session = await _sessions.CreateAsync(
            new SessionCreateOptions
            {
                Mode = "subscription",
                Customer = request.CustomerId,
                LineItems =
                [
                    new SessionLineItemOptions { Price = request.PriceId, Quantity = 1 },
                ],
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,

                // On the session, so checkout.session.completed carries it…
                Metadata = metadata,

                // …and on the subscription Stripe creates, so every later invoice and
                // cancellation event carries it too. Without the second one, a renewal
                // twelve months from now could not be traced back to an account.
                SubscriptionData = new SessionSubscriptionDataOptions { Metadata = metadata },
            },
            new RequestOptions { IdempotencyKey = request.IdempotencyKey },
            cancellationToken);

        return new BillingCheckoutSession(session.Id, session.Url);
    }

    public async Task CancelAtPeriodEndAsync(
        string providerSubscriptionId,
        CancellationToken cancellationToken)
    {
        await _subscriptions.UpdateAsync(
            providerSubscriptionId,
            new SubscriptionUpdateOptions { CancelAtPeriodEnd = true },
            cancellationToken: cancellationToken);
    }

    public async Task<string> CreateBillingPortalUrlAsync(
        string providerCustomerId,
        string returnUrl,
        CancellationToken cancellationToken)
    {
        var session = await _portalSessions.CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = providerCustomerId,
                ReturnUrl = returnUrl,
            },
            cancellationToken: cancellationToken);

        return session.Url;
    }
}
