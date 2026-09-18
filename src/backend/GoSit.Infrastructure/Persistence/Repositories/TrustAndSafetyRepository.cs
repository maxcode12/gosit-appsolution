using GoSit.Application.Abstractions;
using GoSit.Domain.Reviews;
using GoSit.Domain.TrustAndSafety;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

public sealed class ReviewRepository(GoSitDbContext context) : IReviewRepository
{
    public Task<ReviewPair?> FindByBookingIdAsync(
        Guid careBookingId,
        CancellationToken cancellationToken) =>
        context.ReviewPairs
            .Include(p => p.Reviews)
            .SingleOrDefaultAsync(p => p.CareBookingId == careBookingId, cancellationToken);

    public void Add(ReviewPair pair) => context.ReviewPairs.Add(pair);

    public Task<Review?> FindReviewByIdAsync(Guid reviewId, CancellationToken cancellationToken) =>
        context.Reviews.SingleOrDefaultAsync(r => r.Id == reviewId, cancellationToken);

    /// <summary>
    /// The moderation queue. Names both sides, because the decision is about words one
    /// real person wrote about another.
    /// </summary>
    public async Task<IReadOnlyList<ReportedReviewRow>> ListReportedReviewsAsync(
        int take,
        CancellationToken cancellationToken) =>
        await context.Reviews
            .AsNoTracking()
            .Where(r => r.ReportedAtUtc != null
                && r.HiddenAtUtc == null
                && r.ReportDismissedAtUtc == null)
            // Oldest first: a review sitting here is on someone's public profile the
            // whole time it waits.
            .OrderBy(r => r.ReportedAtUtc)
            .Take(take)
            .Select(r => new ReportedReviewRow(
                r.Id,
                r.SubjectProfileId,
                r.SubjectIsCaregiver,
                r.SubjectIsCaregiver
                    ? context.CaregiverProfiles
                        .Where(p => p.Id == r.SubjectProfileId)
                        .Join(context.Users, p => p.UserId, u => u.Id, (p, u) => u.FirstName + " " + u.LastName)
                        .FirstOrDefault()
                    : context.CareRequesterProfiles
                        .Where(p => p.Id == r.SubjectProfileId)
                        .Join(context.Users, p => p.UserId, u => u.Id, (p, u) => u.FirstName + " " + u.LastName)
                        .FirstOrDefault(),
                r.SubjectIsCaregiver
                    ? context.CareRequesterProfiles
                        .Where(p => p.Id == r.AuthorProfileId)
                        .Join(context.Users, p => p.UserId, u => u.Id, (p, u) => u.FirstName + " " + u.LastName)
                        .FirstOrDefault()
                    : context.CaregiverProfiles
                        .Where(p => p.Id == r.AuthorProfileId)
                        .Join(context.Users, p => p.UserId, u => u.Id, (p, u) => u.FirstName + " " + u.LastName)
                        .FirstOrDefault(),
                r.Rating,
                r.Comment,
                r.ReportReason,
                r.ReportedAtUtc!.Value,
                r.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Review>> ListPublishedForSubjectAsync(
        Guid subjectProfileId,
        int take,
        CancellationToken cancellationToken) =>
        await context.Reviews
            .AsNoTracking()
            // Unpublished reviews are invisible to everyone but their author, and hidden
            // ones have been taken down by staff. Both are excluded here rather than
            // filtered later, so no caller can forget.
            .Where(r => r.SubjectProfileId == subjectProfileId
                && r.PublishedAtUtc != null
                && r.HiddenAtUtc == null)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Aggregated in the database rather than by loading every review, so a
    /// well-reviewed caregiver does not get slower to rank as they succeed.
    /// </summary>
    public async Task<(decimal? Average, int Count)> SummarizeForSubjectAsync(
        Guid subjectProfileId,
        CancellationToken cancellationToken)
    {
        var summary = await context.Reviews
            .AsNoTracking()
            // A hidden review stops counting towards the rating. Leaving it in would make
            // taking it down cosmetic, and the rating is the caregiver's income.
            .Where(r => r.SubjectProfileId == subjectProfileId
                && r.PublishedAtUtc != null
                && r.HiddenAtUtc == null)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Average = g.Average(r => (decimal)r.Rating) })
            .SingleOrDefaultAsync(cancellationToken);

        return summary is null
            ? (null, 0)
            : (decimal.Round(summary.Average, 2, MidpointRounding.AwayFromZero), summary.Count);
    }

    public async Task<IReadOnlyList<ReviewPair>> ListAwaitingPublicationAsync(
        DateTime windowClosedBefore,
        int limit,
        CancellationToken cancellationToken) =>
        await context.ReviewPairs
            .Include(p => p.Reviews)
            .Where(p => p.CompletedAtUtc < windowClosedBefore)
            .Where(p => p.Reviews.Any(r => r.PublishedAtUtc == null))
            .OrderBy(p => p.CompletedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
}

public sealed class VerificationRepository(GoSitDbContext context) : IVerificationRepository
{
    public void Add(VerificationCase verificationCase) =>
        context.VerificationCases.Add(verificationCase);

    public Task<VerificationCase?> FindByIdAsync(Guid caseId, CancellationToken cancellationToken) =>
        context.VerificationCases.SingleOrDefaultAsync(c => c.Id == caseId, cancellationToken);

    public async Task<IReadOnlyList<VerificationCase>> ListForCaregiverAsync(
        Guid caregiverProfileId,
        CancellationToken cancellationToken) =>
        await context.VerificationCases
            .Where(c => c.CaregiverProfileId == caregiverProfileId)
            .OrderByDescending(c => c.SubmittedAtUtc)
            .ToListAsync(cancellationToken);

    /// <summary>Oldest first, so nothing sits in the queue indefinitely.</summary>
    public async Task<IReadOnlyList<VerificationCase>> ListPendingAsync(
        int limit,
        CancellationToken cancellationToken) =>
        await context.VerificationCases
            .AsNoTracking()
            .Where(c => c.Status == VerificationCaseStatus.Submitted
                || c.Status == VerificationCaseStatus.InReview)
            .OrderBy(c => c.SubmittedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
}

public sealed class IncidentRepository(GoSitDbContext context) : IIncidentRepository
{
    public void Add(IncidentReport report) => context.IncidentReports.Add(report);

    public Task<IncidentReport?> FindByIdAsync(Guid reportId, CancellationToken cancellationToken) =>
        context.IncidentReports.SingleOrDefaultAsync(r => r.Id == reportId, cancellationToken);

    /// <summary>
    /// Triage order: safety concerns and critical reports first, then oldest. Someone
    /// may be at risk, so queue position is not simply arrival time.
    /// </summary>
    public async Task<IReadOnlyList<IncidentReport>> ListOpenAsync(
        int limit,
        CancellationToken cancellationToken) =>
        await context.IncidentReports
            .AsNoTracking()
            .Where(r => r.Status == IncidentStatus.Open || r.Status == IncidentStatus.UnderReview)
            .OrderByDescending(r => r.Severity == IncidentSeverity.Critical
                || r.Category == IncidentCategory.SafetyConcern)
            .ThenByDescending(r => r.Severity)
            .ThenBy(r => r.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<IncidentReport>> ListRaisedByAsync(
        Guid reportedByUserId,
        CancellationToken cancellationToken) =>
        await context.IncidentReports
            .AsNoTracking()
            .Where(r => r.ReportedByUserId == reportedByUserId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
}

/// <summary>
/// Stands in until a background-check vendor is selected. CLAUDE.md lists that research
/// as outstanding, so this reports that no provider is configured rather than pretending
/// to run a check — cases then wait for a staff decision, which is what actually happens
/// today.
/// </summary>
public sealed class UnconfiguredBackgroundCheckProvider(
    ILogger<UnconfiguredBackgroundCheckProvider> logger) : IBackgroundCheckProvider
{
    public bool IsConfigured => false;

    public Task<string?> InitiateAsync(
        Guid caregiverProfileId,
        VerificationCheckType checkType,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "No background-check provider is configured; {CheckType} for caregiver "
            + "{CaregiverProfileId} awaits a manual decision",
            checkType, caregiverProfileId);

        return Task.FromResult<string?>(null);
    }
}
