using GoSit.Domain.Common;
using GoSit.Domain.Payments.Events;

namespace GoSit.Domain.Payments;

/// <summary>
/// The money side of one booking.
///
/// The rule that shapes this whole aggregate: <b>a payment only succeeds when the
/// provider says so</b>. There is no method a request handler can call to mark a payment
/// succeeded from a client's word — <see cref="MarkSucceeded"/> takes the provider's own
/// reference and is reachable only from verified webhook handling.
///
/// Amounts are decimal. Conversion to the provider's integer minor units happens once,
/// in <see cref="Money"/>.
/// </summary>
public sealed class Payment : Entity
{
    private readonly List<Refund> _refunds = [];

    private Payment()
    {
    }

    public Guid CareBookingId { get; private set; }

    public Guid CareRequesterProfileId { get; private set; }

    public Guid CaregiverProfileId { get; private set; }

    /// <summary>Amount to collect, computed server-side from the booking.</summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = Money.CanadianDollar;

    public PaymentStatus Status { get; private set; }

    /// <summary>The provider's PaymentIntent id. Not a secret, but not a client input either.</summary>
    public string? ProviderPaymentIntentId { get; private set; }

    /// <summary>
    /// Sent to the provider so a retried request cannot charge twice. Derived from the
    /// booking, so the same booking always produces the same key.
    /// </summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Provider failure code, kept for support. Never a card number or secret.</summary>
    public string? FailureCode { get; private set; }

    public decimal RefundedAmount { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? SucceededAtUtc { get; private set; }

    public DateTime? FailedAtUtc { get; private set; }

    /// <summary>Optimistic concurrency: a webhook and a refund must not interleave.</summary>
    public int Version { get; private set; }

    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    public decimal NetAmount => Amount - RefundedAmount;

    public bool IsSettled => Status is PaymentStatus.Succeeded
        or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded;

    public static Payment Create(
        Guid careBookingId,
        Guid careRequesterProfileId,
        Guid caregiverProfileId,
        decimal amount,
        DateTime utcNow)
    {
        if (careBookingId == Guid.Empty)
        {
            throw new DomainException("A booking is required.");
        }

        if (amount <= 0)
        {
            throw new DomainException("A payment amount must be positive.");
        }

        // Rejects an amount that is not a whole number of cents before it ever reaches
        // the provider.
        _ = Money.ToMinorUnits(amount);

        return new Payment
        {
            CareBookingId = careBookingId,
            CareRequesterProfileId = careRequesterProfileId,
            CaregiverProfileId = caregiverProfileId,
            Amount = amount,
            Currency = Money.CanadianDollar,
            Status = PaymentStatus.Pending,
            // Stable per booking, so a retry reuses the provider's original intent
            // instead of creating a second charge.
            IdempotencyKey = $"booking-{careBookingId:N}",
            CreatedAtUtc = utcNow,
        };
    }

    public void AttachProviderIntent(string providerPaymentIntentId)
    {
        if (string.IsNullOrWhiteSpace(providerPaymentIntentId))
        {
            throw new DomainException("A provider reference is required.");
        }

        if (ProviderPaymentIntentId is not null
            && ProviderPaymentIntentId != providerPaymentIntentId)
        {
            throw new DomainException("This payment is already attached to another intent.");
        }

        ProviderPaymentIntentId = providerPaymentIntentId;
    }

    public void MarkProcessing(DateTime utcNow)
    {
        if (Status is not PaymentStatus.Pending)
        {
            return;
        }

        Status = PaymentStatus.Processing;
        Touch();
        _ = utcNow;
    }

    /// <summary>
    /// Confirms the payment. Only ever called from verified provider webhook handling,
    /// never from a client-supplied result.
    ///
    /// Idempotent: a webhook may be delivered more than once, and the second delivery
    /// must not raise a second event or double-count anything.
    /// </summary>
    public void MarkSucceeded(string providerPaymentIntentId, decimal amountReceived, DateTime utcNow)
    {
        AttachProviderIntent(providerPaymentIntentId);

        if (Status is PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded
            or PaymentStatus.Refunded)
        {
            return;
        }

        // The provider is the authority on what was actually collected. A mismatch means
        // the two sides disagree about money, which support must look at.
        if (amountReceived != Amount)
        {
            throw new DomainException(
                "The amount received does not match the amount charged.");
        }

        Status = PaymentStatus.Succeeded;
        SucceededAtUtc = utcNow;
        FailureCode = null;
        Touch();

        RaiseDomainEvent(new PaymentSucceededDomainEvent(
            Id, CareBookingId, CareRequesterProfileId, CaregiverProfileId, Amount, utcNow));
    }

    public void MarkFailed(string? failureCode, DateTime utcNow)
    {
        // A late failure for an already-settled payment is ignored rather than
        // un-settling money that was taken.
        if (IsSettled)
        {
            return;
        }

        Status = PaymentStatus.Failed;
        FailedAtUtc = utcNow;
        FailureCode = Trim(failureCode, 100);
        Touch();

        RaiseDomainEvent(new PaymentFailedDomainEvent(
            Id, CareBookingId, CareRequesterProfileId, failureCode, utcNow));
    }

    public void Cancel(DateTime utcNow)
    {
        if (IsSettled)
        {
            throw new DomainException("A settled payment cannot be cancelled; refund it instead.");
        }

        Status = PaymentStatus.Cancelled;
        Touch();
        _ = utcNow;
    }

    /// <summary>
    /// Records a refund confirmed by the provider. Refunds can be partial and can happen
    /// more than once, so the total is guarded rather than assumed.
    /// </summary>
    public Refund RecordRefund(
        string providerRefundId,
        decimal amount,
        string? reason,
        DateTime utcNow)
    {
        if (!IsSettled)
        {
            throw new DomainException("Only a settled payment can be refunded.");
        }

        if (amount <= 0)
        {
            throw new DomainException("A refund amount must be positive.");
        }

        // Idempotent: the same provider refund arriving twice is not a second refund.
        var existing = _refunds.SingleOrDefault(r => r.ProviderRefundId == providerRefundId);
        if (existing is not null)
        {
            return existing;
        }

        if (RefundedAmount + amount > Amount)
        {
            throw new DomainException("A refund cannot exceed the amount paid.");
        }

        var refund = Refund.Create(Id, providerRefundId, amount, reason, utcNow);
        _refunds.Add(refund);
        RefundedAmount += amount;

        Status = RefundedAmount >= Amount
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;

        Touch();

        RaiseDomainEvent(new RefundCompletedDomainEvent(
            Id, CareBookingId, CareRequesterProfileId, amount, RefundedAmount >= Amount, utcNow));

        return refund;
    }

    public bool IsParticipant(Guid profileId) =>
        profileId == CareRequesterProfileId || profileId == CaregiverProfileId;

    private void Touch() => Version++;

    private static string? Trim(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, maxLength)];
}
