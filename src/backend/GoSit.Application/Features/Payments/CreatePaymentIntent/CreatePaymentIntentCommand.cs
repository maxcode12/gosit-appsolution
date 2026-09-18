using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Bookings;
using GoSit.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Payments.CreatePaymentIntent;

/// <summary>
/// Starts payment for a booking.
///
/// The client sends a booking id and nothing else. The amount is derived from the
/// booking — the actual total once care has happened, otherwise the estimate — so a
/// manipulated request cannot change what is charged.
/// </summary>
public sealed record CreatePaymentIntentCommand(Guid CareBookingId) : ICommand<PaymentIntentResponse>;

public sealed class CreatePaymentIntentHandler(
    IPaymentRepository payments,
    IBookingRepository bookings,
    ICareRequesterProfileRepository requesterProfiles,
    IPaymentProvider provider,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<CreatePaymentIntentHandler> logger)
    : ICommandHandler<CreatePaymentIntentCommand, PaymentIntentResponse>
{
    public async Task<PaymentIntentResponse> HandleAsync(
        CreatePaymentIntentCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var requester = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("That booking does not exist.");

        var booking = await bookings.FindByIdAsync(command.CareBookingId, cancellationToken)
            ?? throw new NotFoundException("That booking does not exist.");

        // Only the requester pays, and only for their own booking. A booking belonging
        // to someone else is reported as not found so ids cannot be probed.
        if (booking.CareRequesterProfileId != requester.Id)
        {
            throw new NotFoundException("That booking does not exist.");
        }

        if (booking.Status is BookingStatus.Declined or BookingStatus.Cancelled)
        {
            throw new ConflictException("This booking is no longer active.");
        }

        var utcNow = clock.UtcNow;

        // Server-derived, never client-supplied: what was actually worked once the
        // booking is complete, otherwise the agreed estimate.
        var amount = booking.ActualTotal ?? booking.EstimatedTotal;

        var payment = await payments.FindByBookingIdAsync(booking.Id, cancellationToken);

        if (payment is null)
        {
            payment = Payment.Create(
                booking.Id, booking.CareRequesterProfileId, booking.CaregiverProfileId,
                amount, utcNow);

            payments.Add(payment);
        }
        else if (payment.IsSettled)
        {
            throw new ConflictException("This booking has already been paid.");
        }

        var intent = await provider.CreatePaymentIntentAsync(
            payment.Amount,
            payment.Currency,
            // Derived from the booking, so a retry reuses the provider's original
            // intent rather than creating a second charge.
            payment.IdempotencyKey,
            new Dictionary<string, string>
            {
                ["bookingId"] = booking.Id.ToString(),
                ["paymentId"] = payment.Id.ToString(),
            },
            cancellationToken);

        payment.AttachProviderIntent(intent.ProviderPaymentIntentId);
        payment.MarkProcessing(utcNow);

        audit.Record(nameof(Payment), payment.Id, "PaymentIntentCreated", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Identifiers only. The client secret must never be logged.
        logger.LogInformation(
            "Payment {PaymentId} for booking {BookingId} attached to intent {PaymentIntentId}",
            payment.Id, booking.Id, intent.ProviderPaymentIntentId);

        return new PaymentIntentResponse(
            payment.Id,
            booking.Id,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            intent.ClientSecret);
    }
}
