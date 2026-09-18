using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Bookings;
using GoSit.Domain.Reviews;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Reviews;

public sealed record SubmitReviewCommand(Guid CareBookingId, int Rating, string? Comment)
    : ICommand<ReviewResponse>;

/// <summary>Published reviews about a caregiver, for their public profile.</summary>
public sealed record ListCaregiverReviewsQuery(Guid CaregiverProfileId, int Take = 20)
    : IQuery<CaregiverReviewsResponse>;

public sealed record ReviewResponse(
    Guid Id,
    Guid CareBookingId,
    int Rating,
    string? Comment,
    DateTime CreatedAtUtc,
    // False while the review is still blind. An author sees their own either way.
    bool IsPublished);

public sealed record CaregiverReviewsResponse(
    decimal? AverageRating,
    int ReviewCount,
    IReadOnlyList<PublishedReview> Reviews);

/// <summary>
/// A published review as shown on a profile. Carries the author's first name and last
/// initial only — never their contact details, and never which booking it came from,
/// which would let a reader correlate a review with a specific date and address.
/// </summary>
public sealed record PublishedReview(
    Guid Id,
    int Rating,
    string? Comment,
    string AuthorFirstName,
    string AuthorLastInitial,
    DateTime CreatedAtUtc);

public sealed class SubmitReviewValidator : AbstractValidator<SubmitReviewCommand>
{
    public SubmitReviewValidator()
    {
        RuleFor(x => x.CareBookingId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(ReviewPolicy.MinimumRating, ReviewPolicy.MaximumRating);
        RuleFor(x => x.Comment).MaximumLength(ReviewPolicy.MaxCommentLength);
    }
}

public sealed class SubmitReviewHandler(
    IReviewRepository reviews,
    IBookingRepository bookings,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    RatingRecalculator ratings,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<SubmitReviewHandler> logger) : ICommandHandler<SubmitReviewCommand, ReviewResponse>
{
    public async Task<ReviewResponse> HandleAsync(
        SubmitReviewCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var booking = await bookings.FindByIdAsync(command.CareBookingId, cancellationToken)
            ?? throw new NotFoundException("That booking does not exist.");

        var authorProfileId = await ResolveParticipantAsync(userId, booking, cancellationToken);

        // Only care that actually happened can be reviewed. A cancelled or declined
        // booking has nothing to review, and a pending one has not happened yet.
        if (booking.Status is not (BookingStatus.Completed or BookingStatus.Disputed))
        {
            throw new ConflictException("Only a completed booking can be reviewed.");
        }

        var utcNow = clock.UtcNow;

        var pair = await reviews.FindByBookingIdAsync(booking.Id, cancellationToken);

        if (pair is null)
        {
            pair = ReviewPair.ForBooking(
                booking.Id,
                booking.CareRequesterProfileId,
                booking.CaregiverProfileId,
                booking.CompletedAtUtc ?? booking.EndAtUtc);

            reviews.Add(pair);
        }

        var review = pair.AddReview(authorProfileId, command.Rating, command.Comment, utcNow);

        audit.Record(nameof(Review), review.Id, "ReviewSubmitted", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Ratings only ever count published reviews, so this does nothing until the
        // pair goes public.
        if (pair.BothSidesHaveReviewed)
        {
            await ratings.RecalculateAsync(pair, utcNow, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Identifiers and the rating only — never the comment.
        logger.LogInformation(
            "Review {ReviewId} submitted for booking {BookingId} with rating {Rating}",
            review.Id, booking.Id, review.Rating);

        return new ReviewResponse(
            review.Id, booking.Id, review.Rating, review.Comment,
            review.CreatedAtUtc, review.IsPublished);
    }

    /// <summary>
    /// Which side of the booking the caller is. Someone who is on neither is told the
    /// booking does not exist, so ids cannot be probed.
    /// </summary>
    private async Task<Guid> ResolveParticipantAsync(
        Guid userId,
        CareBooking booking,
        CancellationToken cancellationToken)
    {
        var caregiver = await caregiverProfiles.FindByUserIdAsync(userId, cancellationToken);

        if (caregiver is not null && booking.IsParticipant(caregiver.Id))
        {
            return caregiver.Id;
        }

        var requester = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken);

        if (requester is not null && booking.IsParticipant(requester.Id))
        {
            return requester.Id;
        }

        throw new NotFoundException("That booking does not exist.");
    }
}

public sealed class ListCaregiverReviewsHandler(
    IReviewRepository reviews,
    ICaregiverSearchRepository caregivers,
    IProfileDirectory profiles) : IQueryHandler<ListCaregiverReviewsQuery, CaregiverReviewsResponse>
{
    private const int MaxTake = 100;

    public async Task<CaregiverReviewsResponse> HandleAsync(
        ListCaregiverReviewsQuery query,
        CancellationToken cancellationToken)
    {
        // Only a listable caregiver's reviews are public, so this cannot be used to
        // probe whether an arbitrary profile exists.
        _ = await caregivers.FindListableByIdAsync(query.CaregiverProfileId, cancellationToken)
            ?? throw new NotFoundException("That caregiver is not available.");

        var take = Math.Clamp(query.Take, 1, MaxTake);

        var published = await reviews.ListPublishedForSubjectAsync(
            query.CaregiverProfileId, take, cancellationToken);

        var (average, count) = await reviews.SummarizeForSubjectAsync(
            query.CaregiverProfileId, cancellationToken);

        var names = await profiles.ListProfileNamesAsync(
            published.Select(r => r.AuthorProfileId).Distinct().ToList(), cancellationToken);

        return new CaregiverReviewsResponse(
            average,
            count,
            published
                .Select(r =>
                {
                    var name = names.GetValueOrDefault(r.AuthorProfileId);

                    return new PublishedReview(
                        r.Id,
                        r.Rating,
                        r.Comment,
                        name?.FirstName ?? "GoSit user",
                        name is { LastName.Length: > 0 } ? name.LastName[..1] : string.Empty,
                        r.CreatedAtUtc);
                })
                .ToList());
    }
}

/// <summary>
/// Recomputes the denormalized rating on a caregiver profile after reviews publish.
///
/// Only published reviews count, so a blind review can never move a rating and reveal
/// itself indirectly.
/// </summary>
public sealed class RatingRecalculator(
    IReviewRepository reviews,
    ICaregiverProfileRepository caregiverProfiles,
    ILogger<RatingRecalculator> logger)
{
    public async Task RecalculateAsync(
        ReviewPair pair,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var caregiver = await caregiverProfiles.FindByIdAsync(
            pair.CaregiverProfileId, cancellationToken);

        if (caregiver is null)
        {
            return;
        }

        var (average, count) = await reviews.SummarizeForSubjectAsync(
            caregiver.Id, cancellationToken);

        caregiver.UpdateRatingSummary(average, count, utcNow);

        logger.LogInformation(
            "Recalculated rating for caregiver {CaregiverProfileId}: {Count} reviews",
            caregiver.Id, count);
    }
}
