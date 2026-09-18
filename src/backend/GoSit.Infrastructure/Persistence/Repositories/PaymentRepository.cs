using GoSit.Application.Abstractions;
using GoSit.Domain.Payments;
using GoSit.Domain.Payouts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GoSit.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(GoSitDbContext context) : IPaymentRepository
{
    public Task<Payment?> FindByBookingIdAsync(Guid careBookingId, CancellationToken cancellationToken) =>
        context.Payments
            .Include(p => p.Refunds)
            .SingleOrDefaultAsync(p => p.CareBookingId == careBookingId, cancellationToken);

    public Task<Payment?> FindByProviderIntentIdAsync(
        string providerPaymentIntentId,
        CancellationToken cancellationToken) =>
        context.Payments
            .Include(p => p.Refunds)
            .SingleOrDefaultAsync(
                p => p.ProviderPaymentIntentId == providerPaymentIntentId, cancellationToken);

    public void Add(Payment payment) => context.Payments.Add(payment);

    /// <summary>
    /// Claims a provider event id. Returns false when it was already recorded.
    ///
    /// Saved immediately rather than with the rest of the unit of work, so the unique
    /// index settles the race when the provider delivers the same event twice at once:
    /// one caller inserts, the other gets a duplicate-key violation and backs off.
    /// </summary>
    public async Task<bool> TryRecordWebhookEventAsync(
        string providerEventId,
        string eventType,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var alreadyProcessed = await context.ProcessedWebhookEvents
            .AnyAsync(e => e.ProviderEventId == providerEventId, cancellationToken);

        if (alreadyProcessed)
        {
            return false;
        }

        context.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent
        {
            ProviderEventId = providerEventId,
            EventType = eventType,
            ProcessedAtUtc = utcNow,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        })
        {
            // A concurrent delivery won. Detach so the failed insert does not poison
            // the rest of this unit of work, and treat the event as already handled.
            foreach (var entry in context.ChangeTracker.Entries<ProcessedWebhookEvent>().ToList())
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }
}

public sealed class PayoutRepository(GoSitDbContext context) : IPayoutRepository
{
    public void Add(Payout payout) => context.Payouts.Add(payout);

    public Task<Payout?> FindByBookingIdAsync(Guid careBookingId, CancellationToken cancellationToken) =>
        context.Payouts.SingleOrDefaultAsync(p => p.CareBookingId == careBookingId, cancellationToken);

    public Task<Payout?> FindByProviderTransferIdAsync(
        string providerTransferId,
        CancellationToken cancellationToken) =>
        context.Payouts.SingleOrDefaultAsync(
            p => p.ProviderTransferId == providerTransferId, cancellationToken);

    public async Task<IReadOnlyList<Payout>> ListForCaregiverAsync(
        Guid caregiverProfileId,
        CancellationToken cancellationToken) =>
        await context.Payouts
            .AsNoTracking()
            .Where(p => p.CaregiverProfileId == caregiverProfileId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Tracked, not AsNoTracking — the sweep mutates and saves these.
    ///
    /// The join filters to caregivers whose account can actually receive money, so
    /// earnings belonging to someone who never finished onboarding cost nothing per
    /// cycle however long they sit there. Oldest first: whoever has waited longest gets
    /// paid first.
    /// </summary>
    public async Task<IReadOnlyList<Payout>> ListSendableAsync(
        int take,
        CancellationToken cancellationToken) =>
        await context.Payouts
            .Where(p => p.Status == PayoutStatus.Queued || p.Status == PayoutStatus.Failed)
            .Where(p => context.CaregiverProfiles.Any(c =>
                c.Id == p.CaregiverProfileId
                && c.PayoutsEnabled
                && c.PayoutAccountId != null))
            .OrderBy(p => p.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Everything owed but not settled: queued, failed and in flight.
    ///
    /// In-transit counts as pending on purpose — the caregiver does not have it yet, and
    /// showing it as paid would be telling them money has arrived when it has not.
    /// </summary>
    public async Task<decimal> SumPendingForCaregiverAsync(
        Guid caregiverProfileId,
        CancellationToken cancellationToken) =>
        await context.Payouts
            .AsNoTracking()
            .Where(p => p.CaregiverProfileId == caregiverProfileId)
            .Where(p => p.Status != PayoutStatus.Paid)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
}
