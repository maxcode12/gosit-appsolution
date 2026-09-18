using GoSit.Domain.Common;
using GoSit.Domain.Payments;
using GoSit.Domain.Payments.Events;

namespace GoSit.Domain.Payouts;

public enum PayoutStatus
{
    /// <summary>Owed to the caregiver, not yet sent.</summary>
    Queued = 1,

    /// <summary>Handed to the provider; the transfer is in flight.</summary>
    InTransit = 2,

    Paid = 3,
    Failed = 4,

    /// <summary>
    /// Never sent, and never will be — the booking was disputed and the family refunded.
    ///
    /// Distinct from Failed, which is a transfer that did not go through and will be
    /// retried. This one is a decision.
    /// </summary>
    Cancelled = 5,
}

/// <summary>
/// Money owed to a caregiver for one completed booking.
///
/// Created only after the corresponding payment has actually settled — GoSit never
/// promises a caregiver money it has not collected. The commission rate is captured
/// here, so a later pricing change cannot alter what an existing payout is worth.
/// </summary>
public sealed class Payout : Entity
{
    private Payout()
    {
    }

    public Guid CaregiverProfileId { get; private set; }

    public Guid CareBookingId { get; private set; }

    public Guid PaymentId { get; private set; }

    /// <summary>What the requester paid for this booking.</summary>
    public decimal GrossAmount { get; private set; }

    public decimal PlatformFee { get; private set; }

    /// <summary>What the caregiver receives. Gross minus the fee.</summary>
    public decimal Amount { get; private set; }

    /// <summary>The commission rate in force when this payout was created.</summary>
    public decimal CommissionRate { get; private set; }

    public string Currency { get; private set; } = Money.CanadianDollar;

    public PayoutStatus Status { get; private set; }

    /// <summary>The provider's transfer reference, once sent.</summary>
    public string? ProviderTransferId { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? PaidAtUtc { get; private set; }

    /// <summary>How many times the sweep has handed this to the provider.</summary>
    public int AttemptCount { get; private set; }

    public DateTime? LastAttemptAtUtc { get; private set; }

    public int Version { get; private set; }

    /// <summary>
    /// How long to wait before retrying a failed transfer, doubling each time and
    /// capped.
    ///
    /// A transfer usually fails for a reason that will not fix itself in an hour — an
    /// account under review, a missing capability — so retrying on every sweep would be
    /// pointless load and a noisy audit trail. It never stops retrying entirely, because
    /// the caregiver has earned the money and the obstacle is usually temporary.
    /// </summary>
    private static readonly TimeSpan[] RetryBackoff =
    [
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(24),
    ];

    /// <summary>
    /// The earliest this may be sent.
    ///
    /// Set from the booking's dispute window. Before this existed a payout went out
    /// within fifteen minutes of the payment settling, while the family had fourteen days
    /// to dispute — so a disputed booking was one whose money had already left, and the
    /// dispute window was decorative.
    /// </summary>
    public DateTime AvailableAtUtc { get; private set; }

    /// <summary>
    /// Held indefinitely because the booking is disputed. Cleared by resolving it.
    ///
    /// Separate from <see cref="AvailableAtUtc"/> because they mean different things to
    /// the caregiver: one is a date to wait for, the other is a decision to wait on, and
    /// an earnings screen that showed them the same way would be lying about one of them.
    /// </summary>
    public bool IsHeldForDispute { get; private set; }

    /// <summary>
    /// Whether the sweep should hand this to the provider now.
    ///
    /// A payout waits out the dispute window, then goes. Failed ones additionally wait
    /// out their backoff. Anything in transit or paid is not the sweep's business, and a
    /// disputed booking's money does not move at all until someone decides.
    /// </summary>
    public bool IsReadyToSend(DateTime utcNow)
    {
        if (IsHeldForDispute || utcNow < AvailableAtUtc)
        {
            return false;
        }

        return Status switch
        {
            PayoutStatus.Queued => true,
            PayoutStatus.Failed => LastAttemptAtUtc is not { } last
                || utcNow - last >= BackoffFor(AttemptCount),
            _ => false,
        };
    }

    /// <summary>
    /// Stops the money moving while a dispute is open.
    ///
    /// Refuses once the transfer is under way. After that the money is with the provider
    /// and holding the record would only make GoSit's books disagree with reality — a
    /// dispute over an already-sent payout is a reversal, which is a different operation
    /// and a different conversation.
    /// </summary>
    public void HoldForDispute()
    {
        if (Status is PayoutStatus.InTransit or PayoutStatus.Paid)
        {
            throw new DomainException(
                "This payout has already been sent and cannot be held.");
        }

        IsHeldForDispute = true;
        Version++;
    }

    /// <summary>Releases a held payout once the dispute is resolved in the caregiver's favour.</summary>
    public void ReleaseFromDispute()
    {
        IsHeldForDispute = false;
        Version++;
    }

    private static TimeSpan BackoffFor(int attempts) =>
        RetryBackoff[Math.Min(Math.Max(attempts - 1, 0), RetryBackoff.Length - 1)];

    /// <summary>
    /// Records that the sweep is about to hand this to the provider.
    ///
    /// Deliberately separate from <see cref="MarkInTransit"/> and called *before* the
    /// provider call: if that call throws, the attempt still counts, so a transfer that
    /// fails by exception backs off exactly like one that fails by response.
    /// </summary>
    public void RecordAttempt(DateTime utcNow)
    {
        AttemptCount++;
        LastAttemptAtUtc = utcNow;
        Version++;
    }

    /// <param name="availableAtUtc">
    /// When the dispute window closes. Required rather than defaulted: a payout created
    /// without one would go out immediately, which is the bug this parameter exists to
    /// make impossible to reintroduce by omission.
    /// </param>
    public static Payout Queue(
        Guid caregiverProfileId,
        Guid careBookingId,
        Guid paymentId,
        FeeSplit split,
        decimal commissionRate,
        DateTime availableAtUtc,
        DateTime utcNow)
    {
        if (caregiverProfileId == Guid.Empty || careBookingId == Guid.Empty || paymentId == Guid.Empty)
        {
            throw new DomainException("A caregiver, booking, and payment are required.");
        }

        if (split.CaregiverEarnings <= 0)
        {
            throw new DomainException("A payout amount must be positive.");
        }

        var payout = new Payout
        {
            CaregiverProfileId = caregiverProfileId,
            CareBookingId = careBookingId,
            PaymentId = paymentId,
            AvailableAtUtc = availableAtUtc,
            GrossAmount = split.Gross,
            PlatformFee = split.PlatformFee,
            Amount = split.CaregiverEarnings,
            CommissionRate = commissionRate,
            Status = PayoutStatus.Queued,
            CreatedAtUtc = utcNow,
        };

        payout.RaiseDomainEvent(new PayoutQueuedDomainEvent(
            payout.Id, caregiverProfileId, split.CaregiverEarnings, utcNow));

        return payout;
    }

    public void MarkInTransit(string providerTransferId)
    {
        if (Status is not PayoutStatus.Queued and not PayoutStatus.Failed)
        {
            throw new DomainException($"A payout that is {Status} cannot be sent.");
        }

        if (string.IsNullOrWhiteSpace(providerTransferId))
        {
            throw new DomainException("A provider transfer reference is required.");
        }

        ProviderTransferId = providerTransferId.Trim();
        Status = PayoutStatus.InTransit;
        FailureReason = null;
        Version++;
    }

    /// <summary>Confirmed by the provider. Idempotent, because webhooks repeat.</summary>
    public void MarkPaid(DateTime utcNow)
    {
        if (Status == PayoutStatus.Paid)
        {
            return;
        }

        Status = PayoutStatus.Paid;
        PaidAtUtc = utcNow;
        FailureReason = null;
        Version++;

        RaiseDomainEvent(new PayoutCompletedDomainEvent(
            Id, CaregiverProfileId, Amount, utcNow));
    }

    /// <summary>
    /// A failed payout stays owed. It returns to a state the sweep can retry rather than
    /// disappearing, because the caregiver has still earned the money.
    /// </summary>
    /// <summary>
    /// Lowers what the caregiver is owed after a partial refund.
    ///
    /// The commission is recomputed from the reduced gross rather than kept: GoSit takes
    /// a share of what a family actually paid, and holding the original fee against a
    /// refunded booking would mean the platform kept its cut of money it gave back.
    /// </summary>
    public void ReduceTo(FeeSplit split, DateTime utcNow)
    {
        if (Status is not PayoutStatus.Queued and not PayoutStatus.Failed)
        {
            throw new DomainException(
                "Only a payout that has not been sent can be reduced.");
        }

        if (split.CaregiverEarnings <= 0)
        {
            throw new DomainException(
                "A payout reduced to nothing should be cancelled instead.");
        }

        if (split.Gross > GrossAmount)
        {
            throw new DomainException("A payout cannot be increased.");
        }

        GrossAmount = split.Gross;
        PlatformFee = split.PlatformFee;
        Amount = split.CaregiverEarnings;
        Version++;
    }

    /// <summary>
    /// Closes a payout that will never be sent, because the family was refunded in full.
    ///
    /// Refuses once the transfer is under way, for the same reason a hold does: the money
    /// is with the provider, and marking it cancelled here would make GoSit's books
    /// disagree with what actually happened.
    /// </summary>
    public void Cancel(string reason, DateTime utcNow)
    {
        if (Status is PayoutStatus.InTransit or PayoutStatus.Paid)
        {
            throw new DomainException(
                "This payout has already been sent and cannot be cancelled.");
        }

        Status = PayoutStatus.Cancelled;
        FailureReason = reason;
        IsHeldForDispute = false;
        Version++;
    }

    public void MarkFailed(string reason)
    {
        if (Status == PayoutStatus.Paid)
        {
            return;
        }

        Status = PayoutStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason)
            ? "The transfer failed."
            : reason.Trim()[..Math.Min(reason.Trim().Length, 500)];
        Version++;
    }
}
