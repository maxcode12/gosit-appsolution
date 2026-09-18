using GoSit.Domain.Common;

namespace GoSit.Domain.Payments;

/// <summary>
/// One refund against a payment, as confirmed by the provider. Created only through
/// <see cref="Payment.RecordRefund"/>, so the running total stays consistent.
/// </summary>
public sealed class Refund : Entity
{
    private Refund()
    {
    }

    public Guid PaymentId { get; private set; }

    /// <summary>The provider's refund id. Also the idempotency key for replayed webhooks.</summary>
    public string ProviderRefundId { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string? Reason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    internal static Refund Create(
        Guid paymentId,
        string providerRefundId,
        decimal amount,
        string? reason,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(providerRefundId))
        {
            throw new DomainException("A provider refund reference is required.");
        }

        return new Refund
        {
            PaymentId = paymentId,
            ProviderRefundId = providerRefundId.Trim(),
            Amount = amount,
            Reason = string.IsNullOrWhiteSpace(reason)
                ? null
                : reason.Trim()[..Math.Min(reason.Trim().Length, 200)],
            CreatedAtUtc = utcNow,
        };
    }
}
