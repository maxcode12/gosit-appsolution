using GoSit.Application.Abstractions;
using GoSit.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

/// <summary>
/// Booking facts for one caregiver's insights.
///
/// A projection rather than a load of the aggregate: insights need six columns, and
/// loading whole bookings would drag every care recipient's details along with them.
/// </summary>
public sealed class CaregiverInsightsRepository(GoSitDbContext context)
    : ICaregiverInsightsRepository
{
    public async Task<IReadOnlyList<CaregiverBookingFact>> ListBookingFactsAsync(
        Guid caregiverProfileId,
        CancellationToken cancellationToken)
    {
        var rows = await context.CareBookings
            .AsNoTracking()
            .Where(b => b.CaregiverProfileId == caregiverProfileId)
            .Select(b => new
            {
                b.CareRequesterProfileId,
                b.Status,
                b.CancelledBy,
                b.CreatedAtUtc,
                b.AcceptedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new CaregiverBookingFact(
                r.CareRequesterProfileId,
                Outcome(r.Status, r.CancelledBy),
                r.CreatedAtUtc,
                r.AcceptedAtUtc))
            .ToList();
    }

    /// <summary>
    /// Turns a status and a canceller into what actually happened.
    ///
    /// The distinction that matters is the last one: a request the platform closed
    /// because it went unanswered is not a decline, and counting it as one would tell a
    /// caregiver they turned down work they were never shown.
    /// </summary>
    private static BookingOutcome Outcome(BookingStatus status, CancellationParty? cancelledBy) =>
        status switch
        {
            BookingStatus.Pending => BookingOutcome.Pending,
            BookingStatus.Accepted => BookingOutcome.Accepted,
            BookingStatus.Declined => BookingOutcome.Declined,
            BookingStatus.InProgress => BookingOutcome.InProgress,
            BookingStatus.Completed => BookingOutcome.Completed,
            BookingStatus.Disputed => BookingOutcome.Disputed,
            BookingStatus.Cancelled => cancelledBy switch
            {
                CancellationParty.Caregiver => BookingOutcome.CancelledByCaregiver,
                CancellationParty.CareRequester => BookingOutcome.CancelledByRequester,
                _ => BookingOutcome.LapsedUnanswered,
            },
            _ => BookingOutcome.Pending,
        };
}
