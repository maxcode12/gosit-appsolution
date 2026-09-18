using GoSit.Domain.Caregivers;
using GoSit.Infrastructure.Persistence;
using GoSit.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Deleting search-exposure counts once they are past their retention period.
///
/// The rows are already anonymous, so this is not damage control — it is the other half
/// of the same decision. Data that exists can be subpoenaed, breached or repurposed, and
/// the cheapest protection for a count nobody needs any more is not to have it.
///
/// The test that earns its place is the boundary one. A sweeper that deletes too little
/// is a slow leak nobody notices; one that deletes too much silently empties the screen
/// the counts exist to fill, and neither failure announces itself.
/// </summary>
[Collection(nameof(ApiCollection))]
public class ExposureRetentionTests(GoSitApiFactory factory)
{
    private static int RetentionDays(GoSitApiFactory factory) =>
        factory.Services.GetRequiredService<IOptions<ExposureRetentionOptions>>().Value.Days;

    /// <summary>Writes a row directly — the sweeper is what is under test, not recording.</summary>
    private async Task<Guid> SeedAsync(params int[] daysAgo)
    {
        var caregiverProfileId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        foreach (var days in daysAgo)
        {
            context.CaregiverExposures.Add(CaregiverExposure.Create(
                caregiverProfileId, today.AddDays(-days), impressions: 1, profileViews: 1));
        }

        await context.SaveChangesAsync();

        return caregiverProfileId;
    }

    private async Task<List<DateOnly>> RemainingAsync(Guid caregiverProfileId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.CaregiverExposures
            .AsNoTracking()
            .Where(e => e.CaregiverProfileId == caregiverProfileId)
            .Select(e => e.OnDate)
            .OrderBy(d => d)
            .ToListAsync();
    }

    private Task<ExposureRetentionResult> SweepAsync() =>
        factory.Services
            .GetRequiredService<ExposureRetentionSweeper>()
            .SweepAsync(CancellationToken.None);

    [Fact]
    public async Task Rows_past_the_retention_period_are_deleted()
    {
        var retention = RetentionDays(factory);
        var caregiverProfileId = await SeedAsync(retention + 30, retention + 1);

        // Asserted before the sweep on purpose. Without it, a seed that quietly wrote
        // nothing would leave an empty table and this test would pass by describing a
        // deletion that never had anything to delete.
        Assert.Equal(2, (await RemainingAsync(caregiverProfileId)).Count);

        await SweepAsync();

        Assert.Empty(await RemainingAsync(caregiverProfileId));
    }

    /// <summary>
    /// The failure that would be invisible: a sweeper that deletes what the screen reads
    /// empties the insights window without anything looking broken.
    /// </summary>
    [Fact]
    public async Task Rows_the_insights_window_still_needs_are_kept()
    {
        // Today, yesterday, and the far edge of the ninety-day window the screen reports.
        var caregiverProfileId = await SeedAsync(0, 1, 89);

        await SweepAsync();

        Assert.Equal(3, (await RemainingAsync(caregiverProfileId)).Count);
    }

    /// <summary>
    /// Exactly on the boundary. An off-by-one here deletes a day the screen is still
    /// counting, or keeps one forever — and both look like working software.
    /// </summary>
    [Fact]
    public async Task The_boundary_day_is_kept_and_the_one_before_it_is_not()
    {
        var retention = RetentionDays(factory);
        var caregiverProfileId = await SeedAsync(retention, retention + 1);

        await SweepAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var remaining = await RemainingAsync(caregiverProfileId);

        Assert.Equal([today.AddDays(-retention)], remaining);
    }

    /// <summary>
    /// Sweeping is idempotent and cheap when there is nothing to do — it runs daily
    /// forever, and the overwhelmingly common outcome is deleting nothing.
    /// </summary>
    [Fact]
    public async Task A_sweep_with_nothing_to_delete_deletes_nothing()
    {
        await SeedAsync(0, 5);

        var first = await SweepAsync();
        var second = await SweepAsync();

        Assert.Equal(0, first.Deleted);
        Assert.Equal(0, second.Deleted);
    }

    /// <summary>One caregiver's expiry must not take another's live counts with it.</summary>
    [Fact]
    public async Task Only_expired_rows_are_touched_across_caregivers()
    {
        var retention = RetentionDays(factory);

        var stale = await SeedAsync(retention + 1);
        var current = await SeedAsync(1);

        var result = await SweepAsync();

        Assert.True(result.Deleted >= 1);
        Assert.Empty(await RemainingAsync(stale));
        Assert.Single(await RemainingAsync(current));
    }

    /// <summary>
    /// Retention must never be configurable below the window the product reports, or the
    /// screen would be reading data the sweeper is deleting underneath it.
    /// </summary>
    [Fact]
    public void The_retention_period_covers_the_reported_window()
    {
        Assert.True(
            RetentionDays(factory) >= 90,
            "Exposure retention must be at least the 90-day insights window.");
    }
}
