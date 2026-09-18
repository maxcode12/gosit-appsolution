using GoSit.Application.Abstractions;
using GoSit.Domain.Subscriptions;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Subscriptions;

/// <summary>
/// Applies a verified subscription webhook.
///
/// This is the only path that can start, renew or end a paid subscription. Nothing a
/// client sends reaches it — the browser returning to a success URL proves nothing, and
/// the subscribe endpoint deliberately does not activate anything when billing is live.
///
/// Deduplication lives in the calling webhook handler, which records the provider's
/// event id before dispatching, so everything here can assume it runs once per event.
/// It is nonetheless written to be re-runnable: every operation it performs is either
/// idempotent in the aggregate or guarded by a state check.
/// </summary>
public sealed class SubscriptionWebhookApplier(
    ISubscriptionRepository subscriptions,
    IUserRepository users,
    IDateTimeProvider clock,
    ILogger<SubscriptionWebhookApplier> logger)
{
    public async Task ApplyAsync(
        PaymentWebhookEvent @event,
        CancellationToken cancellationToken)
    {
        if (@event.Subscription is not { } details)
        {
            return;
        }

        var subscription = await FindAsync(details, cancellationToken);

        switch (@event.Type)
        {
            case PaymentWebhookEventType.SubscriptionActivated:
                await ActivateAsync(details, subscription, cancellationToken);
                break;

            case PaymentWebhookEventType.SubscriptionRenewed:
                Renew(details, subscription);
                break;

            case PaymentWebhookEventType.SubscriptionPaymentFailed:
                MarkPastDue(details, subscription);
                break;

            case PaymentWebhookEventType.SubscriptionCancelled:
                Cancel(details, subscription);
                break;
        }
    }

    /// <summary>
    /// Locates the subscription an event is about.
    ///
    /// By provider id first, because that is exact. Falling back to the account covers
    /// the one case where the provider id is not yet known: the very first activation,
    /// where checkout completed but nothing has been linked yet.
    /// </summary>
    private async Task<UserSubscription?> FindAsync(
        SubscriptionWebhookDetails details,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(details.ProviderSubscriptionId))
        {
            var byProvider = await subscriptions.FindByProviderSubscriptionIdAsync(
                details.ProviderSubscriptionId, cancellationToken);

            if (byProvider is not null)
            {
                return byProvider;
            }
        }

        return details.UserId is { } userId
            ? await subscriptions.FindActiveForUserAsync(userId, cancellationToken)
            : null;
    }

    private async Task ActivateAsync(
        SubscriptionWebhookDetails details,
        UserSubscription? existing,
        CancellationToken cancellationToken)
    {
        if (existing is not null)
        {
            // Already started. Stripe sends both checkout.session.completed and
            // invoice.paid for a new subscription, so this is the ordinary second one:
            // link and sync rather than creating a duplicate.
            LinkAndSync(details, existing);
            return;
        }

        if (details.UserId is not { } userId || string.IsNullOrWhiteSpace(details.PlanCode))
        {
            logger.LogWarning(
                "Ignoring a subscription activation with no account metadata; "
                + "provider subscription {ProviderSubscriptionId}",
                details.ProviderSubscriptionId);

            return;
        }

        var plan = await subscriptions.FindPlanByCodeAsync(details.PlanCode, cancellationToken);

        if (plan is null || plan.IsFree)
        {
            logger.LogWarning(
                "Ignoring a subscription activation for unknown or free plan {PlanCode}",
                details.PlanCode);

            return;
        }

        var user = await users.FindByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("Ignoring a subscription activation for a deleted account");
            return;
        }

        if (!string.IsNullOrWhiteSpace(details.ProviderCustomerId))
        {
            // The account may not have been linked yet if checkout created the customer.
            if (string.IsNullOrWhiteSpace(user.ProviderCustomerId))
            {
                user.LinkProviderCustomer(details.ProviderCustomerId);
            }
        }

        var started = UserSubscription.Start(
            userId, plan, clock.UtcNow, details.ProviderSubscriptionId);

        LinkAndSync(details, started);

        subscriptions.Add(started);

        logger.LogInformation(
            "Subscription started for account {UserId} on plan {PlanCode}", userId, plan.Code);
    }

    private static void Renew(SubscriptionWebhookDetails details, UserSubscription? subscription)
    {
        if (subscription is null)
        {
            return;
        }

        // The provider's period is authoritative, so the period is synced rather than
        // advanced by a month locally. Renew() is still called for the status change and
        // the domain event; SyncBillingPeriod then corrects the dates.
        if (subscription.Status == SubscriptionStatus.PastDue)
        {
            subscription.MarkPaymentRecovered();
        }

        LinkAndSync(details, subscription);
    }

    private static void MarkPastDue(
        SubscriptionWebhookDetails details,
        UserSubscription? subscription)
    {
        // Entitlements continue through the grace period — see UserSubscription.
        // PastDueGrace. Cutting a family off the instant a card expires is the wrong
        // trade, and Stripe is still retrying at this point.
        subscription?.MarkPastDue(DateTime.UtcNow);

        if (subscription is not null)
        {
            LinkAndSync(details, subscription);
        }
    }

    private void Cancel(SubscriptionWebhookDetails details, UserSubscription? subscription)
    {
        if (subscription is null)
        {
            return;
        }

        // The provider has ended it — either the cancellation took effect, or it gave up
        // retrying a card. Either way the paid period is over.
        subscription.CancelImmediately(clock.UtcNow);

        logger.LogInformation(
            "Subscription {ProviderSubscriptionId} ended at the provider",
            details.ProviderSubscriptionId);
    }

    private static void LinkAndSync(
        SubscriptionWebhookDetails details,
        UserSubscription subscription)
    {
        if (!string.IsNullOrWhiteSpace(details.ProviderSubscriptionId)
            && subscription.ProviderSubscriptionId != details.ProviderSubscriptionId)
        {
            subscription.LinkProviderSubscription(details.ProviderSubscriptionId);
        }

        if (details.CurrentPeriodStartUtc is { } start
            && details.CurrentPeriodEndUtc is { } end
            && end > start)
        {
            subscription.SyncBillingPeriod(start, end);
        }
    }
}
