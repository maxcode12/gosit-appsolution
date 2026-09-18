using GoSit.Application.Abstractions;
using GoSit.Application.Features.CareRequesters;
using GoSit.Domain.Bookings;

namespace GoSit.Application.Features.Bookings;

/// <summary>Which side of a booking a response is being built for.</summary>
public enum BookingViewer
{
    CareRequester = 1,
    Caregiver = 2,
}

/// <summary>
/// Builds booking responses for one side of a booking, and decides what that side may see.
///
/// The household details — the exact address, the family's notes, and care notes for any
/// pets — reach a caregiver only once they have accepted. The product said so ("accepting
/// shares their address") and the API did not keep it: every response carried the full
/// address and notes, so a request disclosed where a family lived before anyone had agreed
/// to anything. House sitting made that concrete, because the notes are where an owner
/// writes the door code and the dates the house will be empty.
///
/// The viewer is a required argument rather than inferred, so no caller can build a
/// response without deciding whose it is.
/// </summary>
public sealed class BookingProjector(
    IBookingLookupRepository lookups,
    ICaregiverSearchRepository caregivers,
    IDateTimeProvider clock)
{
    public async Task<BookingResponse> ProjectAsync(
        CareBooking booking,
        BookingViewer viewer,
        CancellationToken cancellationToken)
    {
        var projected = await ProjectManyAsync([booking], viewer, cancellationToken);
        return projected[0];
    }

    public async Task<IReadOnlyList<BookingResponse>> ProjectManyAsync(
        IReadOnlyList<CareBooking> bookings,
        BookingViewer viewer,
        CancellationToken cancellationToken)
    {
        if (bookings.Count == 0)
        {
            return [];
        }

        var today = DateOnly.FromDateTime(clock.UtcNow);

        var services = await lookups.ListServicesAsync(
            bookings.Select(b => b.CareServiceId).Distinct().ToList(), cancellationToken);

        var caregiverUserIds = await lookups.ListCaregiverUserIdsAsync(
            bookings.Select(b => b.CaregiverProfileId).Distinct().ToList(), cancellationToken);

        var careRequesterUserIds = await lookups.ListCareRequesterUserIdsAsync(
            bookings.Select(b => b.CareRequesterProfileId).Distinct().ToList(), cancellationToken);

        // One name lookup covering both sides — it resolves users, not caregivers.
        var names = await caregivers.ListCaregiverNamesAsync(
            caregiverUserIds.Values.Concat(careRequesterUserIds.Values).Distinct().ToList(),
            cancellationToken);

        var recipients = await lookups.ListRecipientsAsync(
            bookings.SelectMany(b => b.Recipients.Select(r => r.CareRecipientId)).Distinct().ToList(),
            cancellationToken);

        var recipientsById = recipients.ToDictionary(r => r.Id);

        // Care notes are loaded only for bookings that may show them, so a pending request
        // never reads what it could not return.
        var disclosedPetIds = bookings
            .Where(b => MayDisclose(b, viewer))
            .SelectMany(b => b.Pets.Select(p => p.PetId))
            .Distinct()
            .ToList();

        var petsById = (await lookups.ListPetsAsync(disclosedPetIds, cancellationToken))
            .ToDictionary(p => p.Id);

        return bookings
            .Select(booking =>
            {
                var service = services.TryGetValue(booking.CareServiceId, out var found)
                    ? new CareServiceDescriptor(found.Code, found.Name)
                    : new CareServiceDescriptor("UNKNOWN", "Care service");

                var caregiver = ResolveCaregiver(booking, caregiverUserIds, names);

                var careRequester = ResolveParticipant(
                    booking.CareRequesterProfileId, careRequesterUserIds, names, "Care requester");

                var bookingRecipients = booking.Recipients
                    .Select(r => recipientsById.TryGetValue(r.CareRecipientId, out var recipient)
                        ? new BookingRecipientSummary(
                            recipient.Id,
                            recipient.FirstName,
                            recipient.AgeOn(today),
                            recipient.RecipientType.ToString(),
                            // Whether needs exist, never what they say.
                            !recipient.CareNeeds.IsEmpty)
                        : new BookingRecipientSummary(
                            r.CareRecipientId, "Care recipient", 0, "Unknown", false))
                    .ToList();

                var disclosed = MayDisclose(booking, viewer);

                // Name, species, breed and size were copied onto the booking and are always
                // shown: a walker deciding whether to accept needs to know it is a large dog.
                var bookingPets = booking.Pets
                    .Select(p => new BookingPetSummary(
                        p.PetId,
                        p.Name,
                        p.Species.ToString(),
                        p.Breed,
                        p.Size?.ToString(),
                        disclosed && petsById.TryGetValue(p.PetId, out var pet) && pet.HasCareNotes
                            ? pet.ToCareNotes()
                            : null))
                    .ToList();

                // For boarding the address is the caregiver's, so who may see it runs the
                // other way from the notes — see CareBooking.AddressDisclosedTo.
                var addressDisclosed = booking.AddressDisclosedTo(viewer == BookingViewer.Caregiver);

                return booking.ToResponse(
                    service, caregiver, careRequester, bookingRecipients, bookingPets, disclosed, addressDisclosed);
            })
            .ToList();
    }

    /// <summary>
    /// The family always sees their own booking in full. A caregiver sees the household
    /// details from acceptance on, and never for a request they declined or that lapsed.
    /// </summary>
    public static bool MayDisclose(CareBooking booking, BookingViewer viewer) =>
        viewer == BookingViewer.CareRequester || booking.HouseholdDetailsDisclosedToCaregiver;

    private static CaregiverDescriptor ResolveCaregiver(
        CareBooking booking,
        IReadOnlyDictionary<Guid, Guid> caregiverUserIds,
        IReadOnlyDictionary<Guid, CaregiverName> names)
    {
        if (caregiverUserIds.TryGetValue(booking.CaregiverProfileId, out var userId)
            && names.TryGetValue(userId, out var name))
        {
            // First name and last initial, matching what search exposes.
            return new CaregiverDescriptor(
                name.FirstName,
                name.LastName.Length > 0 ? name.LastName[..1] : string.Empty);
        }

        return new CaregiverDescriptor("Caregiver", string.Empty);
    }

    private static ParticipantDescriptor ResolveParticipant(
        Guid profileId,
        IReadOnlyDictionary<Guid, Guid> profileUserIds,
        IReadOnlyDictionary<Guid, CaregiverName> names,
        string fallback)
    {
        if (profileUserIds.TryGetValue(profileId, out var userId)
            && names.TryGetValue(userId, out var name))
        {
            return new ParticipantDescriptor(
                name.FirstName,
                name.LastName.Length > 0 ? name.LastName[..1] : string.Empty);
        }

        return new ParticipantDescriptor(fallback, string.Empty);
    }
}
