using GoSit.Application.Abstractions;
using GoSit.Domain.Bookings;
using GoSit.Domain.Notifications;

namespace GoSit.Application.Features.Bookings;

/// <summary>
/// Tells the other side that a booking changed.
///
/// `NotificationType` has declared BookingRequested, Accepted, Declined, Cancelled and
/// Completed since Phase 5, and until now not one of them was ever raised. Only messaging
/// created a notification. So a caregiver was never told a family had booked them, a
/// family was never told their request was accepted or declined, and someone whose
/// caregiver cancelled the night before found out by opening the app.
///
/// Every notification here is a summary and nothing more. They are delivered by email —
/// and later push — so they land in mailboxes and on lock screens, where a care need, an
/// address, or a dispute's contents have no business being. The rule is the same one
/// messaging follows: say that something happened and where to look, never what it said.
/// </summary>
public sealed class BookingNotifier(
    INotificationRepository notifications,
    IBookingLookupRepository lookups)
{
    /// <summary>
    /// Notifies whichever party did not perform the transition.
    ///
    /// The actor is passed in rather than inferred, because both sides can cancel and
    /// telling someone about their own action is noise that trains people to ignore the
    /// ones that matter.
    /// </summary>
    public async Task NotifyAsync(
        CareBooking booking,
        NotificationType type,
        string title,
        string body,
        NotifyWho who,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var caregivers = await lookups.ListCaregiverUserIdsAsync(
            [booking.CaregiverProfileId], cancellationToken);

        var requesters = await lookups.ListCareRequesterUserIdsAsync(
            [booking.CareRequesterProfileId], cancellationToken);

        var recipients = who switch
        {
            NotifyWho.Caregiver => caregivers.Values,
            NotifyWho.CareRequester => requesters.Values,
            _ => caregivers.Values.Concat(requesters.Values),
        };

        foreach (var userId in recipients)
        {
            notifications.Add(Notification.Create(
                userId,
                type,
                title,
                body,
                nameof(CareBooking),
                booking.Id,
                utcNow));
        }
    }
}

/// <summary>Which side of a booking to tell.</summary>
public enum NotifyWho
{
    Caregiver = 1,
    CareRequester = 2,
    Both = 3,
}
