using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Reviews;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Reviews;

/// <summary>
/// Reporting a review, and staff deciding what to do about it.
///
/// A published review was permanent. There was no way to take one down and no way to tell
/// anyone about it — a caregiver's rating is their income, and a retaliatory one-star after
/// a dispute, a comment naming a child, or a slur had no route to a person who could act.
/// `POST /api/v1/reports` reports a *user*, which leaves the words themselves on the
/// profile.
///
/// Both halves are here on purpose. A takedown nobody can ask for helps only the families
/// staff happen to notice, and a report queue nothing can act on is the dispute queue
/// before it had a screen.
/// </summary>
public sealed record ReportReviewCommand(Guid ReviewId, string Reason) : ICommand<Unit>;

public sealed record ListReportedReviewsQuery(int Take = 50)
    : IQuery<IReadOnlyList<ReportedReviewRow>>;

/// <summary>
/// A staff decision. <paramref name="Hide"/> false means the review stands.
/// </summary>
public sealed record ModerateReviewCommand(
    Guid ReviewId,
    bool Hide,
    string? Reason) : ICommand<Unit>;

public sealed class ReportReviewValidator : AbstractValidator<ReportReviewCommand>
{
    public ReportReviewValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(ReviewPolicy.MaxCommentLength);
    }
}

public sealed class ModerateReviewValidator : AbstractValidator<ModerateReviewCommand>
{
    public ModerateReviewValidator()
    {
        // Taking down someone's words needs a stated reason. Letting them stand does not:
        // "this review is within the rules" is the default and adds nothing written down.
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(2000)
            .When(x => x.Hide);
    }
}

public sealed class ReportReviewHandler(
    IReviewRepository reviews,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<ReportReviewCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        ReportReviewCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        // Not found rather than forbidden throughout, so review ids cannot be probed and
        // an unpublished review cannot be confirmed to exist by anyone but its author.
        var notFound = new NotFoundException("That review does not exist.");

        var review = await reviews.FindReviewByIdAsync(command.ReviewId, cancellationToken)
            ?? throw notFound;

        /*
         * Only the person the review is about may report it.
         *
         * Not the author — complaining about your own review is asking staff to unpublish
         * something you chose to write, and the way to withdraw a review is not to have
         * staff adjudicate it. Not a bystander either: a marketplace where anyone can
         * queue up anyone else's reviews is a marketplace where competitors do.
         */
        var requester = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken);
        var caregiver = await caregiverProfiles.FindByUserIdAsync(userId, cancellationToken);

        var isSubject = review.SubjectIsCaregiver
            ? caregiver is not null && caregiver.Id == review.SubjectProfileId
            : requester is not null && requester.Id == review.SubjectProfileId;

        if (!isSubject)
        {
            throw notFound;
        }

        try
        {
            review.Report(command.Reason, clock.UtcNow);
        }
        catch (Domain.Common.DomainException ex)
        {
            throw new ConflictException(ex.Message);
        }

        audit.Record(nameof(Review), review.Id, "ReviewReported", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class ListReportedReviewsHandler(IReviewRepository reviews)
    : IQueryHandler<ListReportedReviewsQuery, IReadOnlyList<ReportedReviewRow>>
{
    public Task<IReadOnlyList<ReportedReviewRow>> HandleAsync(
        ListReportedReviewsQuery query,
        CancellationToken cancellationToken) =>
        reviews.ListReportedReviewsAsync(Math.Clamp(query.Take, 1, 200), cancellationToken);
}

public sealed class ModerateReviewHandler(
    IReviewRepository reviews,
    ICaregiverProfileRepository caregiverProfiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<ModerateReviewHandler> logger) : ICommandHandler<ModerateReviewCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        ModerateReviewCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var review = await reviews.FindReviewByIdAsync(command.ReviewId, cancellationToken)
            ?? throw new NotFoundException("That review does not exist.");

        var utcNow = clock.UtcNow;

        try
        {
            if (command.Hide)
            {
                review.Hide(command.Reason!, userId, utcNow);
            }
            else
            {
                review.DismissReport(utcNow);
            }
        }
        catch (Domain.Common.DomainException ex)
        {
            throw new ConflictException(ex.Message);
        }

        audit.Record(
            nameof(Review), review.Id, command.Hide ? "ReviewHidden" : "ReviewReportDismissed", userId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        /*
         * The rating is recomputed after the hide commits, and this is what makes a
         * takedown mean anything. Without it the review would vanish from the profile
         * while its one star went on dragging the caregiver's average down — a removal
         * that removed only the evidence.
         *
         * Only caregivers carry a rating summary; a review about a family changes no
         * published number, so there is nothing to recompute.
         */
        if (command.Hide && review.SubjectIsCaregiver)
        {
            var caregiver = await caregiverProfiles.FindByIdAsync(
                review.SubjectProfileId, cancellationToken);

            if (caregiver is not null)
            {
                var (average, count) = await reviews.SummarizeForSubjectAsync(
                    caregiver.Id, cancellationToken);

                caregiver.UpdateRatingSummary(average, count, utcNow);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // The decision and the review, never the words: a report is about something one
        // person wrote about another.
        logger.LogInformation(
            "Review {ReviewId} was {Decision} by {UserId}",
            review.Id,
            command.Hide ? "hidden" : "allowed to stand",
            userId);

        return Unit.Value;
    }
}
