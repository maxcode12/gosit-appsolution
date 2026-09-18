using GoSit.Domain.Reviews;
using GoSit.Domain.TrustAndSafety;

namespace GoSit.Application.Abstractions;

public interface IReviewRepository
{
    Task<ReviewPair?> FindByBookingIdAsync(Guid careBookingId, CancellationToken cancellationToken);

    void Add(ReviewPair pair);

    /// <summary>
    /// One review, tracked, for the subject to report or staff to act on.
    /// </summary>
    Task<Review?> FindReviewByIdAsync(Guid reviewId, CancellationToken cancellationToken);

    /// <summary>Reported reviews still waiting on a staff decision, oldest first.</summary>
    Task<IReadOnlyList<ReportedReviewRow>> ListReportedReviewsAsync(
        int take,
        CancellationToken cancellationToken);

    /// <summary>Publicly visible reviews about a profile, newest first.</summary>
    Task<IReadOnlyList<Review>> ListPublishedForSubjectAsync(
        Guid subjectProfileId,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Mean rating and count over publicly visible reviews about a profile.</summary>
    Task<(decimal? Average, int Count)> SummarizeForSubjectAsync(
        Guid subjectProfileId,
        CancellationToken cancellationToken);

    /// <summary>Pairs whose review window has closed but which still hold a blind review.</summary>
    Task<IReadOnlyList<ReviewPair>> ListAwaitingPublicationAsync(
        DateTime windowClosedBefore,
        int limit,
        CancellationToken cancellationToken);
}

/// <summary>
/// A reported review as staff see it in the queue.
///
/// Carries the review's own words and the complaint about them, because the decision is
/// whether those words should stand — there is nothing to withhold here that the subject
/// has not already been shown.
/// </summary>
public sealed record ReportedReviewRow(
    Guid ReviewId,
    Guid SubjectProfileId,
    bool SubjectIsCaregiver,
    string SubjectName,
    string AuthorName,
    int Rating,
    string? Comment,
    string? ReportReason,
    DateTime ReportedAtUtc,
    DateTime CreatedAtUtc);

public interface IVerificationRepository
{
    void Add(VerificationCase verificationCase);

    Task<VerificationCase?> FindByIdAsync(Guid caseId, CancellationToken cancellationToken);

    Task<IReadOnlyList<VerificationCase>> ListForCaregiverAsync(
        Guid caregiverProfileId,
        CancellationToken cancellationToken);

    /// <summary>The staff review queue, oldest first.</summary>
    Task<IReadOnlyList<VerificationCase>> ListPendingAsync(
        int limit,
        CancellationToken cancellationToken);
}

public interface IIncidentRepository
{
    void Add(IncidentReport report);

    Task<IncidentReport?> FindByIdAsync(Guid reportId, CancellationToken cancellationToken);

    /// <summary>The triage queue: urgent first, then oldest.</summary>
    Task<IReadOnlyList<IncidentReport>> ListOpenAsync(int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<IncidentReport>> ListRaisedByAsync(
        Guid reportedByUserId,
        CancellationToken cancellationToken);
}

/// <summary>
/// A third-party criminal record check.
///
/// No provider has been selected — CLAUDE.md lists vendor research as outstanding — so
/// the interface exists to keep the workflow honest about where the boundary is, and the
/// only implementation today records that no provider is configured.
/// </summary>
public interface IBackgroundCheckProvider
{
    bool IsConfigured { get; }

    /// <summary>
    /// Starts a check and returns the provider's reference. Results arrive later, by
    /// webhook or polling, and are applied by a staff decision either way.
    /// </summary>
    Task<string?> InitiateAsync(
        Guid caregiverProfileId,
        VerificationCheckType checkType,
        CancellationToken cancellationToken);
}
