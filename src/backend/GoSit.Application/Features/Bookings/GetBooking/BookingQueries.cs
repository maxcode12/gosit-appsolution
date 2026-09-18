using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.Subscriptions;
using GoSit.Domain.Bookings;
using GoSit.Domain.Subscriptions;

namespace GoSit.Application.Features.Bookings.GetBooking;

public sealed record GetBookingQuery(Guid BookingId) : IQuery<BookingResponse>;

/// <summary>Bookings for the caller, whichever side of the marketplace they are on.</summary>
public sealed record ListBookingsQuery(string? Status, bool UpcomingOnly)
    : IQuery<IReadOnlyList<BookingResponse>>;

public sealed class GetBookingHandler(
    IBookingRepository bookings,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    BookingProjector projector,
    ICurrentUser currentUser) : IQueryHandler<GetBookingQuery, BookingResponse>
{
    public async Task<BookingResponse> HandleAsync(
        GetBookingQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var booking = await bookings.FindByIdAsync(query.BookingId, cancellationToken)
            ?? throw new NotFoundException("That booking does not exist.");

        var requester = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken);
        var caregiver = await caregiverProfiles.FindByUserIdAsync(userId, cancellationToken);

        var isParticipant =
            (requester is not null && booking.IsParticipant(requester.Id))
            || (caregiver is not null && booking.IsParticipant(caregiver.Id));

        // Not a participant is reported as not found, so ids cannot be probed.
        if (!isParticipant)
        {
            throw new NotFoundException("That booking does not exist.");
        }

        // An account is one side or the other, so this is the side the caller is on.
        var viewer = caregiver is not null && booking.IsParticipant(caregiver.Id)
            ? BookingViewer.Caregiver
            : BookingViewer.CareRequester;

        return await projector.ProjectAsync(booking, viewer, cancellationToken);
    }
}

public sealed class ListBookingsHandler(
    IBookingRepository bookings,
    IBookingLookupRepository lookups,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    EntitlementService entitlements,
    BookingProjector projector,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IQueryHandler<ListBookingsQuery, IReadOnlyList<BookingResponse>>
{
    public async Task<IReadOnlyList<BookingResponse>> HandleAsync(
        ListBookingsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        BookingStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<BookingStatus>(query.Status, ignoreCase: true, out var parsed))
            {
                throw new RequestValidationException(
                    new Dictionary<string, string[]>
                    {
                        ["status"] = ["That booking status is not recognized."],
                    });
            }

            status = parsed;
        }

        var filter = new BookingListFilter(status, query.UpcomingOnly);
        var utcNow = clock.UtcNow;

        // The caller's role decides which side of the booking they are listed on. An
        // account is one or the other, so at most one of these matches.
        var caregiver = await caregiverProfiles.FindByUserIdAsync(userId, cancellationToken);

        if (caregiver is not null)
        {
            var forCaregiver = await bookings.ListForCaregiverAsync(
                caregiver.Id, filter, utcNow, cancellationToken);

            return await projector.ProjectManyAsync(
                await PrioritiseRequestsAsync(forCaregiver, cancellationToken),
                BookingViewer.Caregiver,
                cancellationToken);
        }

        var requester = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken);

        if (requester is null)
        {
            // No profile means no bookings — an empty list, not an error.
            return [];
        }

        var forRequester = await bookings.ListForCareRequesterAsync(
            requester.Id, filter, utcNow, cancellationToken);

        return await projector.ProjectManyAsync(forRequester, BookingViewer.CareRequester, cancellationToken);
    }

    /// <summary>
    /// PRIORITY_BOOKING_REQUESTS: a subscriber's unanswered requests are shown to the
    /// caregiver ahead of other unanswered requests.
    ///
    /// Confined to <see cref="BookingStatus.Pending"/>, and only permutes pending entries
    /// among themselves — every other booking keeps its place exactly. A caregiver's
    /// accepted work is their day, and reordering a shift they have already agreed to
    /// because of somebody's plan would make the schedule lie about when to turn up.
    ///
    /// It changes who gets looked at first, never who can be booked. Nothing here filters
    /// a request out, so a request from a family on the free plan is still there, still
    /// answerable, and still visible on the same screen.
    /// </summary>
    private async Task<IReadOnlyList<CareBooking>> PrioritiseRequestsAsync(
        IReadOnlyList<CareBooking> forCaregiver,
        CancellationToken cancellationToken)
    {
        var pendingSlots = forCaregiver
            .Select((booking, index) => (booking, index))
            .Where(entry => entry.booking.Status == BookingStatus.Pending)
            .ToList();

        // Nothing to reorder without at least two requests competing for attention.
        if (pendingSlots.Count < 2)
        {
            return forCaregiver;
        }

        var requesterUserIds = await lookups.ListCareRequesterUserIdsAsync(
            pendingSlots.Select(entry => entry.booking.CareRequesterProfileId).Distinct().ToList(),
            cancellationToken);

        var prioritised = await entitlements.WhoHasAsync(
            requesterUserIds.Values.Distinct().ToList(),
            SubscriptionFeature.PriorityBookingRequests,
            cancellationToken);

        if (prioritised.Count == 0)
        {
            return forCaregiver;
        }

        bool IsPrioritised(CareBooking booking) =>
            requesterUserIds.TryGetValue(booking.CareRequesterProfileId, out var userId)
            && prioritised.Contains(userId);

        // OrderByDescending is stable, so requests that tie keep the order the repository
        // gave them — soonest first.
        var reordered = pendingSlots
            .Select(entry => entry.booking)
            .OrderByDescending(IsPrioritised)
            .ToList();

        var result = forCaregiver.ToArray();

        for (var i = 0; i < pendingSlots.Count; i++)
        {
            result[pendingSlots[i].index] = reordered[i];
        }

        return result;
    }
}
