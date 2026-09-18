using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.Subscriptions;
using GoSit.Domain.Subscriptions;

namespace GoSit.Application.Features.Caregivers.GetInsights;

/// <summary>
/// How a caregiver's own work is going.
///
/// Scoped to the caller — there is no route that takes a caregiver id, so one caregiver
/// cannot read another's figures. Gated on
/// <see cref="SubscriptionFeature.CaregiverInsights"/>.
///
/// Everything here is derived from this caregiver's own bookings and their own reviews.
/// Nothing describes a family: no requester is named, counted by name, or made
/// identifiable, and the repeat-family figure is a count of distinct accounts and
/// nothing else.
/// </summary>
public sealed record GetInsightsQuery : IQuery<CaregiverInsightsResponse>;

/// <summary>
/// One measured window plus the lifetime figures that only make sense whole.
///
/// <see cref="SearchPerformanceAvailable"/> stays in the contract now that it is true.
/// It was the honest half of a promise the plan made and the product could not keep —
/// "see how your profile is performing in search and bookings" with only the bookings
/// half measured — and the flag is what let the screen say so instead of letting the
/// missing half read as a zero. It remains because a deployment whose exposure table is
/// empty is indistinguishable from a caregiver nobody has seen, and the two deserve
/// different sentences.
///
/// What the search figures are, precisely: how many times this caregiver was on a page
/// of results someone was shown, and how many times someone opened their profile. They
/// are counts and nothing else. No family is named, counted by name, or made
/// identifiable — see <see cref="Domain.Caregivers.CaregiverExposure"/> for why that is
/// the design rather than a limitation of it.
/// </summary>
public sealed record CaregiverInsightsResponse(
    int WindowDays,

    // ---- requests, in the window ----
    int RequestsReceived,
    int Accepted,
    int Declined,
    /// <summary>Requests that reached their start time with no answer at all.</summary>
    int LapsedUnanswered,

    /// <summary>
    /// Still open, still answerable.
    ///
    /// Reported separately because it is the difference between a response rate that is
    /// a criticism and one that is simply a work queue. Without it the screen showed
    /// "you replied to 50%" beside "you answered every request" — both derived from the
    /// same figures, and flatly contradicting each other.
    /// </summary>
    int AwaitingYourReply,

    int CancelledByYou,

    /// <summary>
    /// Of the requests answered, the share accepted. Null when none were answered —
    /// a rate out of nothing is not zero percent, it is unknown.
    /// </summary>
    double? AcceptanceRate,

    /// <summary>Of the requests received, the share answered either way.</summary>
    double? ResponseRate,

    /// <summary>
    /// Median minutes from request to acceptance. Median rather than mean, because one
    /// forgotten request would drag an average somewhere unrecognisable.
    ///
    /// Accepted requests only: a decline records no timestamp, so there is nothing
    /// honest to measure for it. Named for exactly what it covers.
    /// </summary>
    int? MedianMinutesToAccept,

    // ---- completed work ----
    int CompletedInWindow,
    int CompletedLifetime,

    /// <summary>
    /// Families who have booked this caregiver more than once. A count of accounts, never
    /// who they are.
    /// </summary>
    int RepeatFamilies,

    // ---- reputation, lifetime ----
    decimal? AverageRating,
    int ReviewCount,

    // ---- search, in the window ----

    /// <summary>
    /// Times this caregiver was on a page of results a family was shown. Being in the
    /// candidate set and not reached does not count.
    /// </summary>
    int SearchAppearances,

    /// <summary>
    /// Times someone opened this caregiver's profile. Excludes the caregiver's own
    /// visits, which would otherwise be most of the number while they set the page up.
    /// </summary>
    int ProfileViews,

    /// <summary>
    /// Of the families who saw them, how many opened the profile — null rather than zero
    /// when they have not been shown at all, for the same reason as the booking rates.
    /// </summary>
    double? ViewRate,

    /// <summary>True now that search appearances are recorded. See the note above.</summary>
    bool SearchPerformanceAvailable = true);

public sealed class GetInsightsHandler(
    ICaregiverInsightsRepository insights,
    ICaregiverExposureRepository exposure,
    ICaregiverProfileRepository caregiverProfiles,
    EntitlementService entitlements,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IQueryHandler<GetInsightsQuery, CaregiverInsightsResponse>
{
    /// <summary>
    /// Long enough that a caregiver working a few times a month has something to look at,
    /// short enough to describe how things are going now rather than last year.
    /// </summary>
    private const int WindowDays = 90;

    public async Task<CaregiverInsightsResponse> HandleAsync(
        GetInsightsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        // Checked before the profile is loaded, so an account without the feature learns
        // nothing about whether a profile exists either way.
        if (!await entitlements.HasAsync(
                userId, SubscriptionFeature.CaregiverInsights, cancellationToken))
        {
            throw new ForbiddenException("Insights are part of a paid plan.");
        }

        var caregiver = await caregiverProfiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("No caregiver profile has been created yet.");

        var utcNow = clock.UtcNow;
        var since = utcNow.AddDays(-WindowDays);

        var facts = await insights.ListBookingFactsAsync(caregiver.Id, cancellationToken);

        var inWindow = facts.Where(f => f.CreatedAtUtc >= since).ToList();

        var accepted = inWindow.Count(f => f.Status == BookingOutcome.Accepted);
        var declined = inWindow.Count(f => f.Status == BookingOutcome.Declined);
        var answered = accepted + declined;

        var completedLifetime = facts.Count(f => f.Status == BookingOutcome.Completed);

        // A booking that completed also got accepted, so completions count as answered
        // for the response figures — otherwise a caregiver who accepted and worked every
        // request would be shown as never having replied.
        var acceptedIncludingWorked = accepted
            + inWindow.Count(f => f.Status is BookingOutcome.Completed or BookingOutcome.InProgress);

        var answeredIncludingWorked = acceptedIncludingWorked + declined;

        // The same ninety days as the booking figures, so the two halves of the screen
        // describe one period rather than two that happen to sit together.
        var seen = await exposure.TotalsSinceAsync(
            caregiver.Id, DateOnly.FromDateTime(since), cancellationToken);

        return new CaregiverInsightsResponse(
            WindowDays,
            inWindow.Count,
            acceptedIncludingWorked,
            declined,
            inWindow.Count(f => f.Status == BookingOutcome.LapsedUnanswered),
            inWindow.Count(f => f.Status == BookingOutcome.Pending),
            inWindow.Count(f => f.Status == BookingOutcome.CancelledByCaregiver),
            Rate(acceptedIncludingWorked, answeredIncludingWorked),
            Rate(answeredIncludingWorked, inWindow.Count),
            MedianMinutesToAccept(inWindow),
            inWindow.Count(f => f.Status == BookingOutcome.Completed),
            completedLifetime,
            RepeatFamilies(facts),
            caregiver.AverageRating,
            caregiver.ReviewCount,
            seen.Impressions,
            seen.ProfileViews,
            Rate(seen.ProfileViews, seen.Impressions));

        // A share of nothing is unknown, not zero.
        static double? Rate(int part, int whole) =>
            whole == 0 ? null : Math.Round((double)part / whole, 3);
    }

    private static int? MedianMinutesToAccept(IReadOnlyList<CaregiverBookingFact> facts)
    {
        var minutes = facts
            .Where(f => f.AcceptedAtUtc is not null)
            .Select(f => (f.AcceptedAtUtc!.Value - f.CreatedAtUtc).TotalMinutes)
            // A clock skew or a backdated fixture could produce a negative interval, and
            // "answered eleven minutes before it was asked" helps nobody.
            .Where(m => m >= 0)
            .OrderBy(m => m)
            .ToList();

        if (minutes.Count == 0)
        {
            return null;
        }

        var middle = minutes.Count / 2;

        var median = minutes.Count % 2 == 1
            ? minutes[middle]
            : (minutes[middle - 1] + minutes[middle]) / 2;

        return (int)Math.Round(median);
    }

    /// <summary>
    /// Families who came back. Counted over all time rather than the window, because a
    /// family returning after four months is exactly the signal this is for.
    /// </summary>
    private static int RepeatFamilies(IReadOnlyList<CaregiverBookingFact> facts) =>
        facts
            .Where(f => f.Status == BookingOutcome.Completed)
            .GroupBy(f => f.CareRequesterProfileId)
            .Count(group => group.Count() > 1);
}
