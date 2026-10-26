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

    /// <summary>The case a provider's message is about, found by the provider's own reference.</summary>
    Task<VerificationCase?> FindByProviderReferenceAsync(
        string providerReference,
        CancellationToken cancellationToken);

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
/// What a screening provider needs to know to start a check. Only the caregiver's email
/// address: the provider emails them directly and collects the rest (consent, identity
/// documents, history) itself, so GoSit never holds it.
/// </summary>
public sealed record BackgroundCheckApplicant(Guid CaregiverProfileId, string Email);

/// <summary>
/// Where a provider says a check has got to. Evidence for a reviewer, not a decision.
/// </summary>
/// <param name="Status">The provider's own word for the progress, for example COMPLETE.</param>
/// <param name="Score">The provider's verdict once it has one, for example CLEAR or REVIEW.</param>
public sealed record BackgroundCheckOutcome(string Status, string? Score);

/// <summary>
/// A third-party screening provider.
///
/// A provider starts a check and reports back; it never decides. Whatever it says is shown
/// to staff beside the case, and only a staff member approves or rejects. Several checks
/// are deliberately left to staff even when a provider is configured (see
/// <see cref="CanInitiate"/>).
/// </summary>
public interface IBackgroundCheckProvider
{
    bool IsConfigured { get; }

    /// <summary>
    /// Whether this provider can run this kind of check. When it cannot, the case simply
    /// waits for staff, exactly as it does with no provider at all. A check the provider has
    /// not confirmed it offers must say false here rather than be guessed at.
    /// </summary>
    bool CanInitiate(VerificationCheckType checkType);

    /// <summary>
    /// Starts a check and returns the provider's reference for it. Results arrive later, by
    /// webhook, and are shown to staff who decide either way.
    /// </summary>
    Task<string?> InitiateAsync(
        BackgroundCheckApplicant applicant,
        VerificationCheckType checkType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asks the provider where a check stands now. Null when the provider does not know the
    /// reference. Used instead of trusting the contents of a webhook, so a repeated or
    /// out-of-order message cannot move a case backwards.
    /// </summary>
    Task<BackgroundCheckOutcome?> GetOutcomeAsync(
        string providerReference,
        CancellationToken cancellationToken);
}
