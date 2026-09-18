using GoSit.Application.Abstractions;
using GoSit.Application.Features.Bookings;
using GoSit.Domain.Bookings;
using GoSit.Domain.CareServices;
using GoSit.Domain.Common;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Scheduling;

/// <summary>What one sweep closed. Counts only, for logging and tests.</summary>
public sealed record BookingSweepResult(int Cancelled, int AutoCompleted)
{
    public int Total => Cancelled + AutoCompleted;
}

/// <summary>
/// Closes bookings the lifecycle would otherwise strand, because no participant is still
/// able to act on them:
///
/// - a booking checked into but never checked out of. Past
///   <see cref="BookingPolicy.MaximumDuration"/> the caregiver's own check-out is refused,
///   so the booking would stay <see cref="BookingStatus.InProgress"/> forever;
/// - a request nobody answered before its start time, which stays
///   <see cref="BookingStatus.Pending"/> and keeps appearing as actionable on both sides.
///
/// Shaped like <see cref="Messaging.OutboxPublisher"/>: scoped resolution per cycle, a
/// poll loop that survives a bad cycle, and logging that carries identifiers only — never
/// an address, instructions, or anything about a care recipient.
/// </summary>
public sealed class BookingSweeper(
    IServiceScopeFactory scopeFactory,
    ILogger<BookingSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Bookings closed per cycle, per reason. A backlog drains over several cycles rather
    /// than in one long run at startup.
    /// </summary>
    private const int BatchSize = 100;

    private const string UnansweredReason =
        "Closed by GoSit: the caregiver did not respond before the booking was due to start.";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Delayed first, unlike the outbox. Nothing here is urgent — a stranded
            // booking has been stranded for hours already — and sweeping before the host
            // has finished starting buys nothing.
            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one bad cycle kill the sweeper; the next poll retries.
                logger.LogError(ex, "The booking sweep failed a cycle and will retry");
            }
        }
    }

    /// <summary>
    /// Runs one sweep. Public so a test — or, later, an admin tool — can trigger it
    /// without waiting for the poll interval.
    /// </summary>
    public async Task<BookingSweepResult> SweepAsync(CancellationToken cancellationToken)
    {
        DateTime utcNow;
        List<Guid> unanswered;
        List<Guid> overrunning;

        using (var scope = scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();
            utcNow = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().UtcNow;

            // Both queries lead with Status and range-scan StartAtUtc, which is exactly
            // the (Status, StartAtUtc) index on care_bookings.
            var unansweredBefore = utcNow - BookingPolicy.UnansweredGrace;

            unanswered = await context.CareBookings
                .Where(b => b.Status == BookingStatus.Pending && b.StartAtUtc <= unansweredBefore)
                .OrderBy(b => b.StartAtUtc)
                .Take(BatchSize)
                .Select(b => b.Id)
                .ToListAsync(cancellationToken);

            // The exact test is on CheckedInAtUtc, which is not indexed. Check-in cannot
            // happen more than CheckInGrace before the start, so a booking past the
            // check-out ceiling necessarily started at least that long ago as well — the
            // StartAtUtc bound is a safe superset that lets the index do the work, with
            // the CheckedInAtUtc predicate filtering on top of it.
            var checkedInBefore = utcNow - BookingPolicy.MaximumDuration;
            var startedBefore = checkedInBefore + BookingPolicy.CheckInGrace;

            // A stay is overrunning only a day past its scheduled pick-up, not fourteen hours
            // after drop-off — otherwise every one would be closed, and billed to its
            // schedule, on its first evening. Mirrors CareBooking.HasOverrun.
            var pickUpBefore = utcNow - BookingPolicy.LatePickupGrace;

            overrunning = await context.CareBookings
                .Where(b => b.Status == BookingStatus.InProgress
                    && ((b.PricingUnit == PricingUnit.Hourly
                            && b.StartAtUtc <= startedBefore
                            && b.CheckedInAtUtc <= checkedInBefore)
                        || (b.PricingUnit == PricingUnit.Nightly
                            && b.EndAtUtc <= pickUpBefore)))
                .OrderBy(b => b.StartAtUtc)
                .Take(BatchSize)
                .Select(b => b.Id)
                .ToListAsync(cancellationToken);
        }

        var cancelled = 0;
        var autoCompleted = 0;

        foreach (var bookingId in unanswered)
        {
            if (await ApplyAsync(bookingId, SweepAction.CancelUnanswered, utcNow, cancellationToken))
            {
                cancelled++;
            }
        }

        foreach (var bookingId in overrunning)
        {
            if (await ApplyAsync(bookingId, SweepAction.AutoComplete, utcNow, cancellationToken))
            {
                autoCompleted++;
            }
        }

        var result = new BookingSweepResult(cancelled, autoCompleted);

        if (result.Total > 0)
        {
            logger.LogInformation(
                "Booking sweep closed {CancelledCount} unanswered and {AutoCompletedCount} overrunning bookings",
                result.Cancelled,
                result.AutoCompleted);
        }

        return result;
    }

    /// <summary>
    /// Applies one closure in its own scope and transaction. Per booking rather than per
    /// batch, so a booking a participant changed in the meantime is skipped on its own
    /// instead of failing the whole cycle's save.
    /// </summary>
    private async Task<bool> ApplyAsync(
        Guid bookingId,
        SweepAction action,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditTrail>();

        var booking = await context.CareBookings
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return false;
        }

        try
        {
            switch (action)
            {
                case SweepAction.CancelUnanswered:
                    // The existing cancel path, attributed to the platform: no participant
                    // asked for this, and Payments must be able to tell it apart from a
                    // cancellation either side chose.
                    booking.Cancel(CancellationParty.Platform, Guid.Empty, UnansweredReason, utcNow);
                    audit.Record(nameof(CareBooking), booking.Id, "BookingCancelledUnanswered", null);
                    break;

                case SweepAction.AutoComplete:
                    booking.AutoComplete(utcNow);
                    audit.Record(nameof(CareBooking), booking.Id, "BookingAutoCompleted", null);

                    // A booking the platform closed is still a completed booking. Whatever
                    // completion is supposed to trigger has to trigger here too, or a
                    // caregiver forgetting to check out would quietly cost their family
                    // a referral.
                    await scope.ServiceProvider
                        .GetRequiredService<BookingCompletionEffects>()
                        .ApplyAsync(booking, utcNow, cancellationToken);
                    break;
            }

            // The domain event lands in the outbox inside this same save, exactly as it
            // does for a transition driven from a request.
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Booking sweep applied {SweepAction} to booking {BookingId}", action, bookingId);

            return true;
        }
        catch (DomainException ex)
        {
            // The booking moved on between the query and the load — a caregiver checked
            // out, someone cancelled. Expected, not a failure. Domain messages are fixed
            // text and carry nothing personal.
            logger.LogInformation(
                "Booking sweep skipped {SweepAction} for booking {BookingId}: {Reason}",
                action,
                bookingId,
                ex.Message);

            return false;
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation(
                "Booking {BookingId} changed during the sweep; leaving it for the next cycle",
                bookingId);

            return false;
        }
    }

    private enum SweepAction
    {
        CancelUnanswered,
        AutoComplete,
    }
}
