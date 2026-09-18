using System.ComponentModel.DataAnnotations;
using GoSit.Application.Abstractions;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoSit.Infrastructure.Scheduling;

/// <summary>How long search-exposure counts are kept.</summary>
public sealed class ExposureRetentionOptions
{
    public const string SectionName = "Insights:ExposureRetention";

    /// <summary>
    /// Days of exposure history to keep.
    ///
    /// OPEN BUSINESS DECISION, like the other numbers of its kind — but not an arbitrary
    /// one. The insights screen reports a ninety-day window, so ninety days is the floor
    /// below which the product stops working. This default is twice that, which leaves
    /// room to compare a window against the one before it without keeping data for a
    /// purpose nobody has named.
    ///
    /// The direction of the argument matters if this is ever revisited: retention should
    /// be justified upwards from what the product does, not set to whatever is cheap to
    /// store. Keeping years of it because disk is cheap is how a counter becomes a
    /// dataset nobody meant to build.
    /// </summary>
    [Range(90, 3650)]
    public int Days { get; set; } = 180;
}

public sealed record ExposureRetentionResult(int Deleted);

/// <summary>
/// Deletes search-exposure counts once they are older than the retention period.
///
/// The rows are already anonymous — counts per caregiver per day, naming no family — so
/// this is not damage control. It is the other half of the same decision: data that
/// exists is data that can be subpoenaed, breached, or quietly repurposed, and the
/// cheapest protection for a figure nobody needs any more is not to have it.
///
/// Deleting is safe because nothing is derived from these rows after the fact. They feed
/// one screen over a fixed window; there is no ledger, no payment, and no dispute that
/// could need last year's impression count as evidence.
/// </summary>
public sealed class ExposureRetentionSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<ExposureRetentionOptions> options,
    ILogger<ExposureRetentionSweeper> logger) : BackgroundService
{
    /// <summary>
    /// Daily. A row that is one day past its retention is not a problem, and a sweep
    /// that runs every few minutes to delete nothing is pure noise in the logs.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// Rows per statement.
    ///
    /// Bounded so the first sweep after a long gap — or after this is deployed to a
    /// database that has been collecting for months — does not take one long lock on a
    /// table that every search writes to.
    /// </summary>
    private const int BatchSize = 5_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
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
                // One bad cycle must not kill the sweeper.
                logger.LogError(ex, "The exposure retention sweep failed and will retry");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Runs one cycle. Public so a test can drive a single deterministic sweep rather
    /// than waiting a day for the poll interval.
    /// </summary>
    public async Task<ExposureRetentionResult> SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var context = provider.GetRequiredService<GoSitDbContext>();
        var utcNow = provider.GetRequiredService<IDateTimeProvider>().UtcNow;

        var cutoff = DateOnly.FromDateTime(utcNow).AddDays(-options.Value.Days);

        var deleted = 0;

        // Batched rather than one statement: `ExecuteDeleteAsync` takes a lock for the
        // duration, and search writes to this table on every request.
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await context.CaregiverExposures
                .Where(e => e.OnDate < cutoff)
                .OrderBy(e => e.OnDate)
                .Take(BatchSize)
                .ExecuteDeleteAsync(cancellationToken);

            deleted += batch;

            if (batch < BatchSize)
            {
                break;
            }
        }

        if (deleted > 0)
        {
            // The cutoff, not the rows: a log line about deleted data should not become
            // a copy of it.
            logger.LogInformation(
                "Deleted {DeletedCount} exposure rows older than {Cutoff:yyyy-MM-dd}",
                deleted,
                cutoff);
        }

        return new ExposureRetentionResult(deleted);
    }
}
