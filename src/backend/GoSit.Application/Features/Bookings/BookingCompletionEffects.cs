using GoSit.Application.Abstractions;
using GoSit.Application.Features.Growth;
using GoSit.Domain.Bookings;

namespace GoSit.Application.Features.Bookings;

/// <summary>
/// What has to happen when a booking reaches <see cref="BookingStatus.Completed"/>,
/// beyond the state change itself.
///
/// Two paths reach that state — a caregiver checking out, and the platform closing a
/// booking nobody checked out of — and an effect wired into only one of them makes the
/// two silently different: a referral would qualify for the family whose caregiver
/// remembered to check out, and not for the family whose caregiver forgot. So both call
/// this, and anything else that comes to depend on completion belongs here rather than
/// in either caller.
///
/// Deliberately runs inside the caller's transaction, before its save. A referral that
/// qualified against a booking whose completion was then rolled back would be a reward
/// for work that never finished.
/// </summary>
public sealed class BookingCompletionEffects(
    IBookingLookupRepository lookups,
    ReferralQualifier referrals)
{
    public async Task ApplyAsync(
        CareBooking booking,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        // Either side of a booking may have been referred: the family who booked, or the
        // caregiver who worked. Both are offered and the qualifier decides — it is the
        // referee's own first completed booking that counts, and it knows which accounts
        // have a referral against them. Neither is the "referred one" from here.
        var caregiverUserIds = await lookups.ListCaregiverUserIdsAsync(
            [booking.CaregiverProfileId], cancellationToken);

        var requesterUserIds = await lookups.ListCareRequesterUserIdsAsync(
            [booking.CareRequesterProfileId], cancellationToken);

        foreach (var userId in caregiverUserIds.Values.Concat(requesterUserIds.Values))
        {
            await referrals.QualifyForCompletedBookingAsync(userId, utcNow, cancellationToken);
        }
    }
}
