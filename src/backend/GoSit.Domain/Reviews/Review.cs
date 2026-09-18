using GoSit.Domain.Common;
using GoSit.Domain.Reviews.Events;

namespace GoSit.Domain.Reviews;

/// <summary>
/// One party's review of the other after a completed booking.
///
/// Reviews are written blind and published together — see
/// <see cref="ReviewPair"/>. A review stays unpublished until the other side has also
/// reviewed, or the window closes. In a care marketplace that matters more than
/// convenience: a caregiver who raises a safety concern, or a family who reports a
/// problem, must not have to weigh a retaliatory review before speaking up.
/// </summary>
public sealed class Review : Entity
{
    private Review()
    {
    }

    public Guid CareBookingId { get; private set; }

    /// <summary>Profile that wrote the review.</summary>
    public Guid AuthorProfileId { get; private set; }

    /// <summary>Profile the review is about.</summary>
    public Guid SubjectProfileId { get; private set; }

    /// <summary>True when a care requester reviewed their caregiver.</summary>
    public bool SubjectIsCaregiver { get; private set; }

    public int Rating { get; private set; }

    public string? Comment { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// When this became visible to the subject. Null while still blind — an unpublished
    /// review must never be returned to anyone but its author.
    /// </summary>
    public DateTime? PublishedAtUtc { get; private set; }

    public bool IsPublished => PublishedAtUtc is not null;

    /// <summary>
    /// When the subject asked staff to look at this review, and what they said.
    ///
    /// Reporting is how staff ever learn a review is abusive. Until this existed a
    /// published review was permanent: a caregiver's rating is their income, and a
    /// retaliatory one-star after a dispute, a comment naming a child, or a slur had no
    /// route to anyone who could act on it.
    /// </summary>
    public DateTime? ReportedAtUtc { get; private set; }

    public string? ReportReason { get; private set; }

    /// <summary>
    /// When staff removed this review from public view.
    ///
    /// Hiding is deliberately not deletion and not an edit. The review stays exactly as
    /// written, because it is evidence — about the author as much as the subject — and a
    /// decision that erased what it was made about could never be reviewed afterwards.
    /// <see cref="PublishedAtUtc"/> is left alone for the same reason: clearing it would
    /// make a decided review look like one still waiting on the other side, and the
    /// publication sweep would put it straight back.
    /// </summary>
    public DateTime? HiddenAtUtc { get; private set; }

    public string? HiddenReason { get; private set; }

    public Guid? HiddenByUserId { get; private set; }

    /// <summary>
    /// Whether this counts in public: published, and not taken down. Ratings and public
    /// listings are both filtered on this.
    /// </summary>
    public bool IsPubliclyVisible => PublishedAtUtc is not null && HiddenAtUtc is null;

    /// <summary>When staff decided the review stands. Kept so it leaves the queue.</summary>
    public DateTime? ReportDismissedAtUtc { get; private set; }

    /// <summary>Still waiting on a staff decision — the moderation queue.</summary>
    public bool AwaitsModeration =>
        ReportedAtUtc is not null && HiddenAtUtc is null && ReportDismissedAtUtc is null;

    /// <summary>
    /// The subject asks staff to look at this review.
    ///
    /// Only a published review can be reported: an unpublished one is invisible to its
    /// subject, so being asked to report one would mean disclosing it early and undoing
    /// the blind-review rule that stops retaliation.
    /// </summary>
    public void Report(string reason, DateTime utcNow)
    {
        if (!IsPublished)
        {
            throw new DomainException("That review is not published.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Say what is wrong with the review.");
        }

        if (reason.Trim().Length > ReviewPolicy.MaxCommentLength)
        {
            throw new DomainException(
                $"A reason must be {ReviewPolicy.MaxCommentLength} characters or fewer.");
        }

        // Reporting twice is not an error — the first report already put it in the queue,
        // and a second complaint from the same person changes nothing about that. The
        // original reason is kept rather than overwritten.
        if (ReportedAtUtc is not null)
        {
            return;
        }

        ReportedAtUtc = utcNow;
        ReportReason = reason.Trim();
    }

    /// <summary>Staff take a review out of public view. The rating is recalculated after.</summary>
    public void Hide(string reason, Guid staffUserId, DateTime utcNow)
    {
        if (!IsPublished)
        {
            throw new DomainException("That review is not published.");
        }

        if (HiddenAtUtc is not null)
        {
            throw new DomainException("That review has already been hidden.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A reason is required.");
        }

        HiddenAtUtc = utcNow;
        HiddenReason = reason.Trim();
        HiddenByUserId = staffUserId;
    }

    /// <summary>
    /// Staff decide the review stands, which clears it from the queue.
    ///
    /// The report is not erased. Whether a review was complained about is part of its
    /// history, and a second complaint about the same review should reach someone who can
    /// see the first was already considered.
    /// </summary>
    public void DismissReport(DateTime utcNow)
    {
        if (ReportedAtUtc is null)
        {
            throw new DomainException("That review was not reported.");
        }

        if (ReportDismissedAtUtc is not null)
        {
            throw new DomainException("That report has already been settled.");
        }

        ReportDismissedAtUtc = utcNow;
    }

    internal static Review Create(
        Guid careBookingId,
        Guid authorProfileId,
        Guid subjectProfileId,
        bool subjectIsCaregiver,
        int rating,
        string? comment,
        DateTime utcNow)
    {
        if (authorProfileId == subjectProfileId)
        {
            throw new DomainException("A review cannot be written about yourself.");
        }

        if (rating is < ReviewPolicy.MinimumRating or > ReviewPolicy.MaximumRating)
        {
            throw new DomainException(
                $"A rating must be between {ReviewPolicy.MinimumRating} and {ReviewPolicy.MaximumRating}.");
        }

        var trimmed = comment?.Trim();

        if (trimmed is { Length: > ReviewPolicy.MaxCommentLength })
        {
            throw new DomainException(
                $"A comment must be {ReviewPolicy.MaxCommentLength} characters or fewer.");
        }

        return new Review
        {
            CareBookingId = careBookingId,
            AuthorProfileId = authorProfileId,
            SubjectProfileId = subjectProfileId,
            SubjectIsCaregiver = subjectIsCaregiver,
            Rating = rating,
            Comment = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed,
            CreatedAtUtc = utcNow,
        };
    }

    internal void Publish(DateTime utcNow)
    {
        if (IsPublished)
        {
            return;
        }

        PublishedAtUtc = utcNow;

        RaiseDomainEvent(new ReviewPublishedDomainEvent(
            Id, CareBookingId, AuthorProfileId, SubjectProfileId,
            SubjectIsCaregiver, Rating, utcNow));
    }
}
