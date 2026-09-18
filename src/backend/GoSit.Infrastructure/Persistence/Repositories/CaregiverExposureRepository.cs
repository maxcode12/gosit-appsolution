using GoSit.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace GoSit.Infrastructure.Persistence.Repositories;

/// <summary>
/// Counting how often caregivers were shown.
///
/// Written with an upsert rather than through the change tracker, for two reasons that
/// both come down to search being the hottest path in the product:
///
/// - **Concurrency.** Two families searching at the same second hit the same daily row.
///   Load-modify-save would lose one of the increments, or need a transaction and a
///   retry, to protect a number nobody is billed on. `ON CONFLICT DO UPDATE` lets the
///   database add them up.
/// - **Cost.** One statement covers a whole page of results, so a search pays one round
///   trip regardless of how many caregivers it returned.
///
/// It runs inline, inside the search request. A background buffer would be cheaper still
/// and is the obvious next step if search ever gets hot, at the price of losing counts on
/// a crash — worth taking for a vanity metric, but not worth the moving parts before
/// there is traffic to justify them.
/// </summary>
public sealed class CaregiverExposureRepository(GoSitDbContext context)
    : ICaregiverExposureRepository
{
    public Task RecordImpressionsAsync(
        IReadOnlyCollection<Guid> caregiverProfileIds,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        // Distinct: a caregiver cannot appear twice on one page, and counting a repeat
        // twice would turn a bug into a flattering number.
        var ids = caregiverProfileIds.Distinct().ToArray();

        return ids.Length == 0
            ? Task.CompletedTask
            : IncrementAsync(ids, onDate, impressions: 1, profileViews: 0, cancellationToken);
    }

    public Task RecordProfileViewAsync(
        Guid caregiverProfileId,
        DateOnly onDate,
        CancellationToken cancellationToken) =>
        IncrementAsync(
            [caregiverProfileId], onDate, impressions: 0, profileViews: 1, cancellationToken);

    /// <summary>
    /// One statement for the whole set: unnest the ids into rows, insert a day's row for
    /// each, and add to whatever is already there.
    ///
    /// Deliberately not <c>ExecuteSqlInterpolated</c> with a loop — the array parameter
    /// keeps this a single round trip and a single plan whatever the page size.
    /// </summary>
    private Task IncrementAsync(
        Guid[] caregiverProfileIds,
        DateOnly onDate,
        int impressions,
        int profileViews,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO caregiver_exposures ("Id", "CaregiverProfileId", "OnDate", "Impressions", "ProfileViews")
            SELECT gen_random_uuid(), id, @onDate, @impressions, @profileViews
            FROM unnest(@ids) AS id
            ON CONFLICT ("CaregiverProfileId", "OnDate") DO UPDATE
            SET "Impressions" = caregiver_exposures."Impressions" + EXCLUDED."Impressions",
                "ProfileViews" = caregiver_exposures."ProfileViews" + EXCLUDED."ProfileViews"
            """;

        return context.Database.ExecuteSqlRawAsync(
            sql,
            [
                new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
                {
                    Value = caregiverProfileIds,
                },
                new NpgsqlParameter("onDate", NpgsqlDbType.Date) { Value = onDate },
                new NpgsqlParameter("impressions", NpgsqlDbType.Integer) { Value = impressions },
                new NpgsqlParameter("profileViews", NpgsqlDbType.Integer) { Value = profileViews },
            ],
            cancellationToken);
    }

    public async Task<CaregiverExposureTotals> TotalsSinceAsync(
        Guid caregiverProfileId,
        DateOnly since,
        CancellationToken cancellationToken)
    {
        // Summed in the database rather than by pulling ninety rows back to add them up.
        var totals = await context.CaregiverExposures
            .AsNoTracking()
            .Where(e => e.CaregiverProfileId == caregiverProfileId && e.OnDate >= since)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Impressions = g.Sum(e => e.Impressions),
                ProfileViews = g.Sum(e => e.ProfileViews),
            })
            .SingleOrDefaultAsync(cancellationToken);

        // No rows is a real zero here, unlike the booking rates: a caregiver who has
        // never been shown has genuinely been shown no times.
        return totals is null
            ? new CaregiverExposureTotals(0, 0)
            : new CaregiverExposureTotals(totals.Impressions, totals.ProfileViews);
    }
}
