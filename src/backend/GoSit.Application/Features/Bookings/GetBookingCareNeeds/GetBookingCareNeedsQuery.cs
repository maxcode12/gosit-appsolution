using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRequesters;
using GoSit.Domain.Bookings;

namespace GoSit.Application.Features.Bookings.GetBookingCareNeeds;

/// <summary>
/// The recorded care needs of one recipient named on one booking.
///
/// This is the only route by which a caregiver ever reads a care-needs profile, and it
/// is deliberately narrow. Four things must all hold:
///
///   1. The caller is a participant in the booking.
///   2. The recipient is actually named on that booking.
///   3. The booking has been taken on — accepted or later. A pending or declined
///      request discloses nothing, so a caregiver cannot fish for a child's medical
///      detail by receiving requests they never intend to accept.
///   4. Every read is written to the audit trail, because who opened a child's record
///      and when is exactly the question an incident investigation asks.
///
/// Anything else is reported as not found rather than forbidden, so the existence of a
/// recipient or a booking cannot be probed.
/// </summary>
public sealed record GetBookingCareNeedsQuery(Guid BookingId, Guid CareRecipientId)
    : IQuery<BookingCareNeedsResponse>;

/// <summary>
/// Care needs as disclosed to a booked caregiver.
///
/// Carries the recipient's first name and age only — the same shape the booking already
/// shows. A last name adds nothing to the care and would widen what a caregiver learns
/// about a family beyond what the job requires.
/// </summary>
public sealed record BookingCareNeedsResponse(
    Guid CareRecipientId,
    string FirstName,
    int Age,
    string RecipientType,
    CareNeedsResponse CareNeeds);

public sealed class GetBookingCareNeedsHandler(
    IBookingRepository bookings,
    IBookingLookupRepository lookups,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IQueryHandler<GetBookingCareNeedsQuery, BookingCareNeedsResponse>
{
    /// <summary>
    /// States in which care has actually been taken on. Pending is excluded on purpose:
    /// a request is not consent to read a care record.
    /// </summary>
    private static readonly BookingStatus[] DisclosingStates =
    [
        BookingStatus.Accepted,
        BookingStatus.InProgress,
        BookingStatus.Completed,
        BookingStatus.Disputed,
    ];

    public async Task<BookingCareNeedsResponse> HandleAsync(
        GetBookingCareNeedsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var notFound = new NotFoundException("Those care needs are not available.");

        var booking = await bookings.FindByIdAsync(query.BookingId, cancellationToken)
            ?? throw notFound;

        var requester = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken);
        var caregiver = await caregiverProfiles.FindByUserIdAsync(userId, cancellationToken);

        var isRequester = requester is not null && booking.IsParticipant(requester.Id);
        var isCaregiver = caregiver is not null && booking.IsParticipant(caregiver.Id);

        if (!isRequester && !isCaregiver)
        {
            throw notFound;
        }

        if (!booking.Recipients.Any(r => r.CareRecipientId == query.CareRecipientId))
        {
            throw notFound;
        }

        // The requester owns the record and may always read it. A caregiver may read it
        // only once they have taken the booking on.
        if (isCaregiver && !DisclosingStates.Contains(booking.Status))
        {
            throw notFound;
        }

        var recipients = await lookups.ListRecipientsAsync([query.CareRecipientId], cancellationToken);
        var recipient = recipients.FirstOrDefault() ?? throw notFound;

        audit.Record(
            "CareNeedsProfile",
            recipient.Id,
            isCaregiver ? "DisclosedToCaregiver" : "ReadByOwner",
            userId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new BookingCareNeedsResponse(
            recipient.Id,
            recipient.FirstName,
            recipient.AgeOn(DateOnly.FromDateTime(clock.UtcNow)),
            recipient.RecipientType.ToString(),
            recipient.CareNeeds.ToResponse());
    }
}
