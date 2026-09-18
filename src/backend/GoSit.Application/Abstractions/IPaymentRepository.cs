using GoSit.Domain.Payments;
using GoSit.Domain.Payouts;

namespace GoSit.Application.Abstractions;

public interface IPaymentRepository
{
    Task<Payment?> FindByBookingIdAsync(Guid careBookingId, CancellationToken cancellationToken);

    /// <summary>
    /// Looks a payment up by the provider's intent id. This is how webhook handling
    /// finds the payment an event refers to.
    /// </summary>
    Task<Payment?> FindByProviderIntentIdAsync(
        string providerPaymentIntentId,
        CancellationToken cancellationToken);

    void Add(Payment payment);

    /// <summary>
    /// Records that a provider event has been handled. Returns false when the id was
    /// already recorded, which is how repeat deliveries are ignored.
    /// </summary>
    Task<bool> TryRecordWebhookEventAsync(
        string providerEventId,
        string eventType,
        DateTime utcNow,
        CancellationToken cancellationToken);
}

public interface IPayoutRepository
{
    void Add(Payout payout);

    Task<Payout?> FindByBookingIdAsync(Guid careBookingId, CancellationToken cancellationToken);

    Task<Payout?> FindByProviderTransferIdAsync(
        string providerTransferId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Payout>> ListForCaregiverAsync(
        Guid caregiverProfileId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Payouts the sweep should try to send: queued or failed, and belonging to a
    /// caregiver whose connected account can actually receive money.
    ///
    /// Filtering on the account here rather than in the sweep means a caregiver who has
    /// not finished onboarding costs nothing per cycle, however long their earnings sit
    /// waiting.
    /// </summary>
    Task<IReadOnlyList<Payout>> ListSendableAsync(
        int take,
        CancellationToken cancellationToken);

    /// <summary>What a caregiver is owed but has not been sent — queued, failed or in flight.</summary>
    Task<decimal> SumPendingForCaregiverAsync(
        Guid caregiverProfileId,
        CancellationToken cancellationToken);
}
