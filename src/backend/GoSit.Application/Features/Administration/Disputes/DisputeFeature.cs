using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Bookings;
using GoSit.Domain.Notifications;
using GoSit.Domain.Payments;
using GoSit.Domain.Payouts;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Administration.Disputes;

/// <summary>
/// Staff settlement of disputed bookings.
///
/// A dispute holds the caregiver's payout — that is what makes the dispute window mean
/// anything, since before it the money left within fifteen minutes of the payment
/// settling. But a hold with no release is money stranded indefinitely, and until this
/// existed a disputed booking was invisible to staff: not in the incident queue, not
/// anywhere. Nobody could see it and nobody could end it.
///
/// The decision is a single number — how much of what the family paid goes back to them.
/// Everything else follows from it: nothing refunded means the caregiver is paid in full,
/// everything refunded means they are paid nothing, and in between the payout is
/// recomputed on what the family actually kept paying for.
/// </summary>
public sealed record ListOpenDisputesQuery(int Take = 50)
    : IQuery<IReadOnlyList<OpenDispute>>;

public sealed record OpenDispute(
    Guid CareBookingId,
    string CareRequesterName,
    string CaregiverName,
    string CareServiceName,
    DateTime StartAtUtc,
    DateTime? CompletedAtUtc,
    DateTime DisputedAtUtc,

    /// <summary>What the disputing party said. Their words, shown to staff unedited.</summary>
    string? Reason,

    /// <summary>What the family paid, and so the most that can go back to them.</summary>
    decimal AmountPaid,

    /// <summary>What the caregiver is owed if the dispute is dismissed.</summary>
    decimal PayoutAmount,

    /// <summary>
    /// False when the money has already gone — a transfer that left before the dispute
    /// was raised. Staff can still refund the family, but it comes out of GoSit rather
    /// than out of the payout, and that is a different conversation to have knowingly.
    /// </summary>
    bool PayoutIsRecoverable);

/// <summary>
/// Settles a dispute by deciding what goes back to the family.
///
/// Zero is a real answer, not a missing one: it means the dispute was heard and
/// dismissed, and the caregiver is paid.
/// </summary>
public sealed record ResolveDisputeCommand(
    Guid CareBookingId,
    decimal RefundAmount,
    string Notes) : ICommand<Unit>;

public sealed class ResolveDisputeValidator : AbstractValidator<ResolveDisputeCommand>
{
    public ResolveDisputeValidator()
    {
        RuleFor(x => x.RefundAmount).GreaterThanOrEqualTo(0);

        // Both parties are told the outcome. "Your dispute was closed" with no reason is
        // worse than no message at all, and this is the only place the reason comes from.
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(2000);
    }
}

public sealed class ResolveDisputeHandler(
    IBookingRepository bookings,
    IPaymentRepository payments,
    IPayoutRepository payouts,
    IPaymentProvider provider,
    PlatformFeePolicy feePolicy,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    Bookings.BookingNotifier notifier,
    ILogger<ResolveDisputeHandler> logger) : ICommandHandler<ResolveDisputeCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        ResolveDisputeCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var booking = await bookings.FindByIdAsync(command.CareBookingId, cancellationToken)
            ?? throw new NotFoundException("That booking does not exist.");

        var payment = await payments.FindByBookingIdAsync(booking.Id, cancellationToken);
        var payout = await payouts.FindByBookingIdAsync(booking.Id, cancellationToken);

        var paid = payment?.NetAmount ?? 0m;

        if (command.RefundAmount > paid)
        {
            throw new ConflictException(
                $"The family paid {paid:C}, so no more than that can be refunded.");
        }

        var utcNow = clock.UtcNow;

        // Recorded first so the decision exists even if the provider call below fails —
        // and refuses a second settlement, so a retry cannot refund twice.
        booking.ResolveDispute(command.Notes, utcNow);

        if (command.RefundAmount > 0 && payment is not null)
        {
            await RefundAsync(payment, command.RefundAmount, booking.Id, utcNow, cancellationToken);
        }

        if (payout is not null)
        {
            SettlePayout(payout, paid - command.RefundAmount, utcNow);
        }

        /*
         * Both sides, and in the same breath as the decision itself.
         *
         * The staff note is required precisely so there is something to send, and the
         * admin form tells whoever writes it that both people will read it. Recording a
         * mandatory explanation and delivering it to nobody would make that a lie told to
         * the staff member as well as a silence to the two people waiting on an answer.
         *
         * The note is not repeated here. It goes on the booking, where both of them read
         * the same words; this travels by email and lands on lock screens, and a dispute
         * is about something that happened in a family's home.
         */
        await notifier.NotifyAsync(
            booking,
            NotificationType.DisputeResolved,
            "Your dispute has been settled",
            "Open GoSit to read the decision.",
            Bookings.NotifyWho.Both,
            utcNow,
            cancellationToken);

        audit.Record(nameof(CareBooking), booking.Id, "DisputeResolved", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // The decision and the booking, never the reason or the notes: a dispute is
        // about something that happened in a family's home.
        logger.LogInformation(
            "Dispute on booking {CareBookingId} settled by {UserId} with a refund of {RefundAmount}",
            booking.Id,
            userId,
            command.RefundAmount);

        return Unit.Value;
    }

    /// <summary>
    /// Refunds the family through the provider, then records it.
    ///
    /// The provider first: recording a refund we never made would tell a family their
    /// money is coming when it is not, and that is the failure worth ordering against.
    /// The reverse — a refund the provider made that we failed to record — is recoverable
    /// from the webhook, which reconciles refunds independently.
    /// </summary>
    private async Task RefundAsync(
        Payment payment,
        decimal amount,
        Guid bookingId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        const string reason = "Refunded after a disputed booking.";

        var refund = await provider.RefundAsync(
            payment.ProviderPaymentIntentId!,
            amount,
            reason,
            // Keyed on the booking, so a staff member who presses twice, or a retry after
            // a timeout, cannot refund a family's money a second time.
            $"dispute-refund:{bookingId}",
            cancellationToken);

        payment.RecordRefund(refund.ProviderRefundId, refund.Amount, reason, utcNow);
    }

    /// <summary>
    /// Brings the payout into line with what the family actually paid for.
    ///
    /// A refund of everything cancels it. A partial refund recomputes the split on what
    /// is left — including GoSit's commission, because the platform should not keep its
    /// cut of money it handed back. Nothing refunded simply releases the hold.
    ///
    /// A payout already sent cannot be touched; the aggregate refuses, and the queue tells
    /// staff so before they decide.
    /// </summary>
    private void SettlePayout(Payout payout, decimal remaining, DateTime utcNow)
    {
        if (payout.Status is PayoutStatus.InTransit or PayoutStatus.Paid)
        {
            // Already gone. The refund above still stands — it comes out of GoSit rather
            // than out of the caregiver, which is why the queue flags this in advance.
            return;
        }

        if (remaining <= 0)
        {
            payout.Cancel("The booking was disputed and the family refunded.", utcNow);
            return;
        }

        var split = feePolicy.Split(remaining, hasReducedCommission: payout.CommissionRate < feePolicy.CommissionRate);

        if (split.Gross < payout.GrossAmount)
        {
            payout.ReduceTo(split, utcNow);
        }

        payout.ReleaseFromDispute();
    }
}

public sealed class ListOpenDisputesHandler(
    IDisputeRepository disputes,
    IDateTimeProvider clock)
    : IQueryHandler<ListOpenDisputesQuery, IReadOnlyList<OpenDispute>>
{
    public async Task<IReadOnlyList<OpenDispute>> HandleAsync(
        ListOpenDisputesQuery query,
        CancellationToken cancellationToken)
    {
        _ = clock;

        return await disputes.ListOpenAsync(
            Math.Clamp(query.Take, 1, 200), cancellationToken);
    }
}
