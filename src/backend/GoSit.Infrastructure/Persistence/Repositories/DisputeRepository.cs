using GoSit.Application.Abstractions;
using GoSit.Application.Features.Administration.Disputes;
using GoSit.Domain.Bookings;
using GoSit.Domain.Payouts;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

/// <summary>
/// The staff queue of disputed bookings.
///
/// One projection joining the booking, both parties, the payment and the payout, because
/// the decision needs all of them at once: how much the family paid bounds the refund,
/// and whether the payout has already left decides whose money a refund comes out of.
/// </summary>
public sealed class DisputeRepository(GoSitDbContext context) : IDisputeRepository
{
    public async Task<IReadOnlyList<OpenDispute>> ListOpenAsync(
        int take,
        CancellationToken cancellationToken)
    {
        return await context.CareBookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.Disputed && b.DisputeResolvedAtUtc == null)
            // Oldest first: a dispute sitting here is a caregiver unpaid and a family
            // unanswered at the same time.
            .OrderBy(b => b.DisputedAtUtc)
            .Take(take)
            .Select(b => new OpenDispute(
                b.Id,
                context.CareRequesterProfiles
                    .Where(p => p.Id == b.CareRequesterProfileId)
                    .Join(context.Users, p => p.UserId, u => u.Id, (p, u) => u.FirstName + " " + u.LastName)
                    .FirstOrDefault() ?? "Unknown",
                context.CaregiverProfiles
                    .Where(p => p.Id == b.CaregiverProfileId)
                    .Join(context.Users, p => p.UserId, u => u.Id, (p, u) => u.FirstName + " " + u.LastName)
                    .FirstOrDefault() ?? "Unknown",
                context.CareServices
                    .Where(s => s.Id == b.CareServiceId)
                    .Select(s => s.Name)
                    .FirstOrDefault() ?? "Care",
                b.StartAtUtc,
                b.CompletedAtUtc,
                b.DisputedAtUtc ?? b.CreatedAtUtc,
                b.ResolutionReason,
                context.Payments
                    .Where(p => p.CareBookingId == b.Id)
                    .Select(p => p.Amount - p.RefundedAmount)
                    .FirstOrDefault(),
                context.Payouts
                    .Where(p => p.CareBookingId == b.Id)
                    .Select(p => p.Amount)
                    .FirstOrDefault(),
                // Recoverable only while the transfer has not left.
                context.Payouts
                    .Where(p => p.CareBookingId == b.Id)
                    .Select(p => p.Status == PayoutStatus.Queued || p.Status == PayoutStatus.Failed)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }
}
