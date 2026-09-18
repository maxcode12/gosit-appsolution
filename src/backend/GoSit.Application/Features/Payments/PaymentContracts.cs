using GoSit.Domain.Payments;

namespace GoSit.Application.Features.Payments;

/// <summary>
/// Returned when a requester starts paying.
///
/// <see cref="ClientSecret"/> lets the browser confirm this one payment. It is returned
/// only to the requester who owns the booking, and is never logged or persisted in a
/// response cache.
/// </summary>
public sealed record PaymentIntentResponse(
    Guid PaymentId,
    Guid CareBookingId,
    decimal Amount,
    string Currency,
    string Status,
    string ClientSecret);

/// <summary>
/// The receipt for a booking. Carries no card details — GoSit never sees a card number,
/// and the provider's client secret has no place in a receipt.
/// </summary>
public sealed record PaymentReceiptResponse(
    Guid PaymentId,
    Guid CareBookingId,
    decimal Amount,
    decimal RefundedAmount,
    decimal NetAmount,
    string Currency,
    string Status,
    DateTime? SucceededAtUtc,
    IReadOnlyList<RefundResponse> Refunds);

public sealed record RefundResponse(decimal Amount, string? Reason, DateTime CreatedAtUtc);

public static class PaymentMapping
{
    public static PaymentReceiptResponse ToReceipt(this Payment payment) =>
        new(
            payment.Id,
            payment.CareBookingId,
            payment.Amount,
            payment.RefundedAmount,
            payment.NetAmount,
            payment.Currency,
            payment.Status.ToString(),
            payment.SucceededAtUtc,
            payment.Refunds
                .OrderBy(r => r.CreatedAtUtc)
                .Select(r => new RefundResponse(r.Amount, r.Reason, r.CreatedAtUtc))
                .ToList());
}
