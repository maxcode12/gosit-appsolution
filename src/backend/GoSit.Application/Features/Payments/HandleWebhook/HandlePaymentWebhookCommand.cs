using GoSit.Application.Abstractions;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Subscriptions;
using GoSit.Domain.Payments;
using GoSit.Domain.Bookings;
using GoSit.Domain.Payouts;
using GoSit.Domain.Subscriptions;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Payments.HandleWebhook;

/// <summary>
/// Applies a verified provider webhook.
///
/// This is the only path that can mark a payment succeeded. Nothing a client sends can
/// reach it: the raw payload and signature are verified before this handler runs, and a
/// failure to verify never gets here at all.
/// </summary>
public sealed record HandlePaymentWebhookCommand(string Payload, string? SignatureHeader)
    : ICommand<Unit>;

public sealed class HandlePaymentWebhookHandler(
    IPaymentWebhookParser parser,
    IPaymentRepository payments,
    IPayoutRepository payouts,
    IBookingRepository bookings,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    PayoutFactory payoutFactory,
    Subscriptions.SubscriptionWebhookApplier subscriptionWebhooks,
    Payouts.PayoutAccountWebhookApplier payoutAccountWebhooks,
    ILogger<HandlePaymentWebhookHandler> logger) : ICommandHandler<HandlePaymentWebhookCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        HandlePaymentWebhookCommand command,
        CancellationToken cancellationToken)
    {
        // Throws WebhookVerificationException on a bad signature or a stale timestamp.
        // The API maps that to 400 without disclosing why.
        var @event = parser.Parse(command.Payload, command.SignatureHeader);

        var utcNow = clock.UtcNow;

        // Deliveries repeat, and two separate events can describe the same change.
        // Recording the provider's event id first makes the whole handler idempotent.
        var isNew = await payments.TryRecordWebhookEventAsync(
            @event.EventId, @event.Type.ToString(), utcNow, cancellationToken);

        if (!isNew)
        {
            logger.LogInformation(
                "Ignoring webhook {ProviderEventId}; it has already been processed", @event.EventId);

            return Unit.Value;
        }

        switch (@event.Type)
        {
            case PaymentWebhookEventType.PaymentSucceeded:
                await ApplyPaymentSucceededAsync(@event, utcNow, cancellationToken);
                break;

            case PaymentWebhookEventType.PaymentFailed:
                await ApplyPaymentFailedAsync(@event, utcNow, cancellationToken);
                break;

            case PaymentWebhookEventType.RefundCompleted:
                await ApplyRefundAsync(@event, utcNow, cancellationToken);
                break;

            case PaymentWebhookEventType.SubscriptionActivated:
            case PaymentWebhookEventType.SubscriptionRenewed:
            case PaymentWebhookEventType.SubscriptionPaymentFailed:
            case PaymentWebhookEventType.SubscriptionCancelled:
                // Subscription state belongs to the subscriptions feature. Dispatched
                // from here rather than given its own endpoint because the provider
                // delivers everything to one URL, and deduplication — already done
                // above — must happen in exactly one place.
                await subscriptionWebhooks.ApplyAsync(@event, cancellationToken);
                break;

            case PaymentWebhookEventType.PayoutAccountUpdated:
                // Connect account state belongs to the payouts feature. Dispatched from
                // here for the same reason as subscriptions: one endpoint, and
                // deduplication happens in exactly one place.
                await payoutAccountWebhooks.ApplyAsync(@event, cancellationToken);
                break;

            case PaymentWebhookEventType.PayoutPaid:
                await ApplyPayoutPaidAsync(@event, utcNow, cancellationToken);
                break;

            case PaymentWebhookEventType.PayoutFailed:
                await ApplyPayoutFailedAsync(@event, cancellationToken);
                break;

            default:
                logger.LogInformation(
                    "Acknowledged unhandled webhook {ProviderEventId}", @event.EventId);
                break;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    private async Task ApplyPaymentSucceededAsync(
        PaymentWebhookEvent @event,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var payment = await FindPaymentAsync(@event, cancellationToken);

        if (payment is null || @event.Amount is not { } amountReceived)
        {
            return;
        }

        payment.MarkSucceeded(@event.ProviderPaymentIntentId!, amountReceived, utcNow);

        logger.LogInformation("Payment {PaymentId} succeeded", payment.Id);

        // The caregiver is owed their share only once the money has actually landed.
        await payoutFactory.QueueForAsync(payment, utcNow, cancellationToken);
    }

    private async Task ApplyPaymentFailedAsync(
        PaymentWebhookEvent @event,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var payment = await FindPaymentAsync(@event, cancellationToken);

        payment?.MarkFailed(@event.FailureCode, utcNow);

        if (payment is not null)
        {
            // The failure code is a provider classification, safe to log. Card details
            // never reach GoSit at all.
            logger.LogInformation(
                "Payment {PaymentId} failed with {FailureCode}", payment.Id, @event.FailureCode);
        }
    }

    private async Task ApplyRefundAsync(
        PaymentWebhookEvent @event,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var payment = await FindPaymentAsync(@event, cancellationToken);

        if (payment is null || @event.Amount is not { } refundedTotal)
        {
            return;
        }

        // Stripe reports the running total refunded on the charge, not the delta, so
        // the new portion is whatever exceeds what is already recorded.
        var newlyRefunded = refundedTotal - payment.RefundedAmount;

        if (newlyRefunded <= 0)
        {
            return;
        }

        payment.RecordRefund(
            @event.ProviderRefundId ?? $"{@event.EventId}-refund",
            newlyRefunded,
            reason: null,
            utcNow);

        logger.LogInformation("Payment {PaymentId} refunded {Amount}", payment.Id, newlyRefunded);
    }

    private async Task ApplyPayoutPaidAsync(
        PaymentWebhookEvent @event,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (@event.ProviderTransferId is not { } transferId)
        {
            return;
        }

        var payout = await payouts.FindByProviderTransferIdAsync(transferId, cancellationToken);
        payout?.MarkPaid(utcNow);
    }

    private async Task ApplyPayoutFailedAsync(
        PaymentWebhookEvent @event,
        CancellationToken cancellationToken)
    {
        if (@event.ProviderTransferId is not { } transferId)
        {
            return;
        }

        var payout = await payouts.FindByProviderTransferIdAsync(transferId, cancellationToken);
        // Stays owed and retryable: the caregiver has still earned it.
        payout?.MarkFailed("The provider reported the transfer as failed.");
    }

    private async Task<Payment?> FindPaymentAsync(
        PaymentWebhookEvent @event,
        CancellationToken cancellationToken)
    {
        if (@event.ProviderPaymentIntentId is not { } intentId)
        {
            return null;
        }

        var payment = await payments.FindByProviderIntentIdAsync(intentId, cancellationToken);

        if (payment is null)
        {
            // An event for something GoSit does not know about. Acknowledged so the
            // provider stops retrying, but logged because it should not happen.
            logger.LogWarning(
                "Webhook {ProviderEventId} referenced unknown payment intent {PaymentIntentId}",
                @event.EventId, intentId);
        }

        return payment;
    }
}

/// <summary>
/// Creates the payout owed to a caregiver once a payment settles.
///
/// Separate from the webhook handler so the fee split has one home and can be unit
/// tested without a webhook.
/// </summary>
public sealed class PayoutFactory(
    IPayoutRepository payouts,
    IBookingLookupRepository bookings,
    ICaregiverProfileRepository caregiverProfiles,
    EntitlementService entitlements,
    PlatformFeePolicy feePolicy,
    ILogger<PayoutFactory> logger)
{
    public async Task QueueForAsync(Payment payment, DateTime utcNow, CancellationToken cancellationToken)
    {
        // One payout per booking. A repeat webhook must not owe the caregiver twice.
        var existing = await payouts.FindByBookingIdAsync(payment.CareBookingId, cancellationToken);

        if (existing is not null)
        {
            return;
        }

        /*
         * REDUCED_COMMISSION, resolved at the moment the payout is created.
         *
         * The rate is then stamped on the payout, so a caregiver who cancels their plan
         * next week still keeps the rate that applied to the work they have already
         * done — and one who subscribes tomorrow does not retroactively get it. The
         * alternative, resolving at send time, would let a plan change rewrite what
         * someone earned for a shift they have already worked.
         */
        var hasReducedCommission = await HasReducedCommissionAsync(
            payment.CaregiverProfileId, cancellationToken);

        var split = feePolicy.Split(payment.Amount, hasReducedCommission);

        /*
         * Held until the family can no longer dispute.
         *
         * Payouts used to go out within fifteen minutes of the payment settling, while a
         * family had fourteen days to raise a dispute — so by the time anyone disputed, the
         * money was already with the caregiver and there was nothing left to refund from.
         * The dispute window existed and could not be honoured.
         *
         * Measured from completion rather than from now: the window a family is told about
         * runs from when the care finished, and paying out fourteen days after a late
         * settlement would extend the wait for a reason the caregiver never sees.
         */
        var completedAt = await bookings.FindCompletedAtAsync(
            payment.CareBookingId, cancellationToken) ?? utcNow;

        var availableAt = completedAt + BookingPolicy.DisputeWindow;

        var payout = Payout.Queue(
            payment.CaregiverProfileId,
            payment.CareBookingId,
            payment.Id,
            split,
            feePolicy.RateFor(hasReducedCommission),
            availableAt,
            utcNow);

        payouts.Add(payout);

        logger.LogInformation(
            "Queued payout {PayoutId} of {Amount} for caregiver {CaregiverProfileId}",
            payout.Id, payout.Amount, payout.CaregiverProfileId);
    }

    /// <summary>
    /// Whether this caregiver's plan reduces the platform's cut.
    ///
    /// A profile that has since been deleted falls back to the standard rate rather than
    /// throwing: the payment has already been taken, and refusing to queue what someone
    /// is owed because their profile is gone would strand the money.
    /// </summary>
    private async Task<bool> HasReducedCommissionAsync(
        Guid caregiverProfileId,
        CancellationToken cancellationToken)
    {
        var profile = await caregiverProfiles.FindByIdAsync(caregiverProfileId, cancellationToken);

        if (profile is null)
        {
            logger.LogWarning(
                "Caregiver profile {CaregiverProfileId} was not found while pricing a payout; "
                + "the standard commission applies",
                caregiverProfileId);

            return false;
        }

        return await entitlements.HasAsync(
            profile.UserId, SubscriptionFeature.ReducedCommission, cancellationToken);
    }
}
