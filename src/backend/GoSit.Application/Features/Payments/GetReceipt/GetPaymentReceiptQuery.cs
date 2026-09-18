using GoSit.Application.Abstractions;
using GoSit.Application.Common;

namespace GoSit.Application.Features.Payments.GetReceipt;

/// <summary>
/// The receipt for a booking. Visible to both participants — the requester needs proof
/// of what they paid, the caregiver of what was collected.
/// </summary>
public sealed record GetPaymentReceiptQuery(Guid CareBookingId) : IQuery<PaymentReceiptResponse>;

public sealed class GetPaymentReceiptHandler(
    IPaymentRepository payments,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    ICurrentUser currentUser) : IQueryHandler<GetPaymentReceiptQuery, PaymentReceiptResponse>
{
    public async Task<PaymentReceiptResponse> HandleAsync(
        GetPaymentReceiptQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var payment = await payments.FindByBookingIdAsync(query.CareBookingId, cancellationToken)
            ?? throw new NotFoundException("There is no payment for that booking.");

        var requester = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken);
        var caregiver = await caregiverProfiles.FindByUserIdAsync(userId, cancellationToken);

        var isParticipant =
            (requester is not null && payment.IsParticipant(requester.Id))
            || (caregiver is not null && payment.IsParticipant(caregiver.Id));

        // Not a participant is reported as not found, so booking ids cannot be probed
        // for whether a payment exists.
        if (!isParticipant)
        {
            throw new NotFoundException("There is no payment for that booking.");
        }

        return payment.ToReceipt();
    }
}
