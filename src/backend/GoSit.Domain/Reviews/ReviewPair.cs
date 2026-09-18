using GoSit.Domain.Common;

namespace GoSit.Domain.Reviews;

/// <summary>
/// The two reviews for one booking, and the rules that govern them.
///
/// Aggregate root rather than two loose reviews, because the interesting rules are about
/// the <i>pair</i>: one review each, blind until both are in, and published together.
/// Keeping that here means no handler can publish a review early by accident.
/// </summary>
public sealed class ReviewPair : Entity
{
    private readonly List<Review> _reviews = [];

    private ReviewPair()
    {
    }

    public Guid CareBookingId { get; private set; }

    public Guid CareRequesterProfileId { get; private set; }

    public Guid CaregiverProfileId { get; private set; }

    /// <summary>When the booking completed. The review window runs from here.</summary>
    public DateTime CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<Review> Reviews => _reviews.AsReadOnly();

    public bool BothSidesHaveReviewed => _reviews.Count == 2;

    public static ReviewPair ForBooking(
        Guid careBookingId,
        Guid careRequesterProfileId,
        Guid caregiverProfileId,
        DateTime completedAtUtc) =>
        new()
        {
            CareBookingId = careBookingId,
            CareRequesterProfileId = careRequesterProfileId,
            CaregiverProfileId = caregiverProfileId,
            CompletedAtUtc = completedAtUtc,
        };

    public bool IsParticipant(Guid profileId) =>
        profileId == CareRequesterProfileId || profileId == CaregiverProfileId;

    public bool WindowIsOpenAt(DateTime utcNow) =>
        utcNow - CompletedAtUtc <= ReviewPolicy.Window;

    public Review? ReviewBy(Guid authorProfileId) =>
        _reviews.SingleOrDefault(r => r.AuthorProfileId == authorProfileId);

    /// <summary>
    /// Reviews visible to the given profile: everything published, plus their own,
    /// which an author can always see even while it is still blind.
    /// </summary>
    public IEnumerable<Review> VisibleTo(Guid profileId) =>
        _reviews.Where(r => r.IsPublished || r.AuthorProfileId == profileId);

    public Review AddReview(Guid authorProfileId, int rating, string? comment, DateTime utcNow)
    {
        if (!IsParticipant(authorProfileId))
        {
            throw new DomainException("Only a participant can review this booking.");
        }

        if (!WindowIsOpenAt(utcNow))
        {
            throw new DomainException("The window for reviewing this booking has closed.");
        }

        if (ReviewBy(authorProfileId) is not null)
        {
            throw new DomainException("You have already reviewed this booking.");
        }

        var authorIsCaregiver = authorProfileId == CaregiverProfileId;

        var review = Review.Create(
            CareBookingId,
            authorProfileId,
            authorIsCaregiver ? CareRequesterProfileId : CaregiverProfileId,
            subjectIsCaregiver: !authorIsCaregiver,
            rating,
            comment,
            utcNow);

        _reviews.Add(review);

        // The second review makes both visible at once, so neither side can read the
        // other's before committing to their own.
        if (BothSidesHaveReviewed)
        {
            PublishAll(utcNow);
        }

        return review;
    }

    /// <summary>
    /// Publishes once the window has closed, so a single review is not hidden forever
    /// because the other side never responded. Called by the scheduled sweep.
    /// </summary>
    public bool PublishIfWindowClosed(DateTime utcNow)
    {
        if (WindowIsOpenAt(utcNow) || _reviews.All(r => r.IsPublished))
        {
            return false;
        }

        PublishAll(utcNow);
        return true;
    }

    private void PublishAll(DateTime utcNow)
    {
        foreach (var review in _reviews.Where(r => !r.IsPublished))
        {
            review.Publish(utcNow);

            // Domain events are raised on the child but collected from the aggregate
            // root, so they are forwarded here.
            foreach (var raised in review.DomainEvents)
            {
                RaiseDomainEvent(raised);
            }

            review.ClearDomainEvents();
        }
    }
}
