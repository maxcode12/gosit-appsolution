using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRequesters;
using GoSit.Domain.Bookings;
using GoSit.Domain.CareRequesters;

namespace GoSit.Application.Features.Bookings.GetBookingEmergencyContacts;

/// <summary>
/// Who to ring when the family cannot be reached during a booking.
///
/// `EmergencyContact` said from the start that contact details are "released only to a
/// caregiver on a confirmed booking", and nothing ever released them: the caregiver had no
/// route to this at all. Someone alone in a house with a child and an unanswered phone had
/// the recorded emergency *instructions* and no person to call.
///
/// It follows the same four rules as
/// <see cref="GetBookingCareNeeds.GetBookingCareNeedsQuery"/>, deliberately, because it is
/// the same kind of disclosure — a family's private contacts handed to someone because of
/// a job they took on:
///
///   1. The caller is a participant in the booking.
///   2. The booking has been taken on — accepted or later. A pending request is not
///      consent to learn a family's phone numbers, or every caregiver could harvest them
///      by receiving requests they never intend to accept.
///   3. Every read is audited, because who was given a family's contacts and when is what
///      an incident investigation asks.
///   4. Anything else is not found rather than forbidden, so nothing can be probed.
///
/// One difference from care needs, and it is the point of the feature: this stays readable
/// through `Completed`. A caregiver may need to reach someone about what happened during a
/// booking that has just ended, and a rule that cut them off at check-out would take the
/// phone number away at exactly the moment it is wanted.
/// </summary>
public sealed record GetBookingEmergencyContactsQuery(Guid BookingId)
    : IQuery<BookingEmergencyContactsResponse>;

/// <summary>
/// The family's emergency contacts as disclosed on a booking.
///
/// Carries a name, a relationship and a number — nothing else is any of a caregiver's
/// business, and the relationship is what decides who to ring first.
/// </summary>
public sealed record BookingEmergencyContactsResponse(
    IReadOnlyList<EmergencyContactResponse> Contacts);

public sealed class GetBookingEmergencyContactsHandler(
    IBookingRepository bookings,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IQueryHandler<GetBookingEmergencyContactsQuery, BookingEmergencyContactsResponse>
{
    /// <summary>
    /// States in which care has actually been taken on. Pending and Declined are excluded
    /// on purpose; Completed is included on purpose — see the note on the query.
    /// </summary>
    private static readonly BookingStatus[] DisclosingStates =
    [
        BookingStatus.Accepted,
        BookingStatus.InProgress,
        BookingStatus.Completed,
        BookingStatus.Disputed,
    ];

    public async Task<BookingEmergencyContactsResponse> HandleAsync(
        GetBookingEmergencyContactsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var notFound = new NotFoundException("Those contacts are not available.");

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

        if (isCaregiver && !DisclosingStates.Contains(booking.Status))
        {
            throw notFound;
        }

        // Read from the booking's own requester rather than from the caller, so a
        // caregiver gets the contacts of the family who booked them and nobody else's.
        // The repository returns the contacts alone rather than the profile — see
        // ICareRequesterProfileRepository for why that distinction is kept.
        var contacts = await requesterProfiles.ListEmergencyContactsAsync(
            booking.CareRequesterProfileId, cancellationToken);

        audit.Record(
            nameof(EmergencyContact),
            booking.CareRequesterProfileId,
            isCaregiver ? "DisclosedToCaregiver" : "ReadByOwner",
            userId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new BookingEmergencyContactsResponse(
            contacts
                .OrderBy(c => c.CreatedAtUtc)
                .Select(c => c.ToResponse())
                .ToList());
    }
}
