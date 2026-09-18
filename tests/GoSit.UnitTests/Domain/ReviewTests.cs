using GoSit.Domain.Common;
using GoSit.Domain.Reviews;
using GoSit.Domain.Reviews.Events;
using Xunit;

namespace GoSit.UnitTests.Domain;

public class ReviewPairTests
{
    private static readonly DateTime Completed = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Completed.AddHours(1);
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Caregiver = Guid.NewGuid();

    private static ReviewPair Pair() =>
        ReviewPair.ForBooking(Guid.NewGuid(), Requester, Caregiver, Completed);

    [Fact]
    public void A_participant_can_review_after_the_booking_completes()
    {
        var pair = Pair();

        var review = pair.AddReview(Requester, 5, "Wonderful with Sam.", Now);

        Assert.Equal(5, review.Rating);
        Assert.Equal(Requester, review.AuthorProfileId);
        Assert.Equal(Caregiver, review.SubjectProfileId);
        Assert.True(review.SubjectIsCaregiver);
    }

    [Fact]
    public void A_caregiver_review_is_about_the_requester()
    {
        var pair = Pair();

        var review = pair.AddReview(Caregiver, 4, "Clear instructions.", Now);

        Assert.Equal(Requester, review.SubjectProfileId);
        Assert.False(review.SubjectIsCaregiver);
    }

    [Fact]
    public void Someone_outside_the_booking_cannot_review_it()
    {
        var pair = Pair();

        Assert.Throws<DomainException>(() => pair.AddReview(Guid.NewGuid(), 5, null, Now));
    }

    [Fact]
    public void A_participant_can_only_review_once()
    {
        var pair = Pair();
        pair.AddReview(Requester, 5, null, Now);

        Assert.Throws<DomainException>(() => pair.AddReview(Requester, 1, "Changed my mind.", Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void An_out_of_range_rating_is_rejected(int rating)
    {
        var pair = Pair();

        Assert.Throws<DomainException>(() => pair.AddReview(Requester, rating, null, Now));
    }

    [Fact]
    public void An_over_long_comment_is_rejected()
    {
        var pair = Pair();

        Assert.Throws<DomainException>(() => pair.AddReview(
            Requester, 5, new string('x', ReviewPolicy.MaxCommentLength + 1), Now));
    }

    [Fact]
    public void A_review_written_after_the_window_closes_is_rejected()
    {
        var pair = Pair();
        var tooLate = Completed + ReviewPolicy.Window + TimeSpan.FromDays(1);

        Assert.Throws<DomainException>(() => pair.AddReview(Requester, 5, null, tooLate));
    }

    // ---- blind review: the property that makes reviews safe to write ----

    /// <summary>
    /// A caregiver who raises a safety concern, or a family who reports a problem, must
    /// not have to weigh a retaliatory review before speaking up. So the first review
    /// stays hidden.
    /// </summary>
    [Fact]
    public void The_first_review_is_not_published()
    {
        var pair = Pair();

        var review = pair.AddReview(Requester, 5, "Wonderful.", Now);

        Assert.False(review.IsPublished);
        Assert.Empty(pair.DomainEvents);
    }

    [Fact]
    public void An_author_can_see_their_own_unpublished_review()
    {
        var pair = Pair();
        pair.AddReview(Requester, 5, "Wonderful.", Now);

        Assert.Single(pair.VisibleTo(Requester));
        // The subject cannot see it yet.
        Assert.Empty(pair.VisibleTo(Caregiver));
    }

    [Fact]
    public void The_second_review_publishes_both_at_once()
    {
        var pair = Pair();
        pair.AddReview(Requester, 5, "Wonderful.", Now);

        pair.AddReview(Caregiver, 4, "Clear instructions.", Now.AddHours(1));

        Assert.All(pair.Reviews, r => Assert.True(r.IsPublished));
        Assert.Equal(2, pair.VisibleTo(Requester).Count());
        Assert.Equal(2, pair.VisibleTo(Caregiver).Count());
    }

    [Fact]
    public void Publishing_raises_an_event_for_each_review()
    {
        var pair = Pair();
        pair.AddReview(Requester, 5, null, Now);
        pair.AddReview(Caregiver, 4, null, Now.AddHours(1));

        Assert.Equal(2, pair.DomainEvents.Count);
        Assert.All(pair.DomainEvents, e => Assert.IsType<ReviewPublishedDomainEvent>(e));
    }

    /// <summary>
    /// A single review must not stay hidden forever because the other side never
    /// responded — otherwise silence would suppress feedback.
    /// </summary>
    [Fact]
    public void A_lone_review_publishes_once_the_window_closes()
    {
        var pair = Pair();
        pair.AddReview(Requester, 5, "Wonderful.", Now);

        var afterWindow = Completed + ReviewPolicy.Window + TimeSpan.FromMinutes(1);
        var published = pair.PublishIfWindowClosed(afterWindow);

        Assert.True(published);
        Assert.True(pair.Reviews.Single().IsPublished);
        Assert.Single(pair.VisibleTo(Caregiver));
    }

    [Fact]
    public void The_window_sweep_does_nothing_while_the_window_is_open()
    {
        var pair = Pair();
        pair.AddReview(Requester, 5, null, Now);

        Assert.False(pair.PublishIfWindowClosed(Now.AddHours(1)));
        Assert.False(pair.Reviews.Single().IsPublished);
    }

    [Fact]
    public void The_window_sweep_is_idempotent()
    {
        var pair = Pair();
        pair.AddReview(Requester, 5, null, Now);
        var afterWindow = Completed + ReviewPolicy.Window + TimeSpan.FromMinutes(1);

        Assert.True(pair.PublishIfWindowClosed(afterWindow));
        Assert.False(pair.PublishIfWindowClosed(afterWindow.AddHours(1)));
    }

    [Fact]
    public void A_pair_with_no_reviews_needs_no_publishing()
    {
        var pair = Pair();
        var afterWindow = Completed + ReviewPolicy.Window + TimeSpan.FromMinutes(1);

        Assert.False(pair.PublishIfWindowClosed(afterWindow));
    }
}
