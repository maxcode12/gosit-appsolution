using GoSit.Application.Features.CareRequesters;
using GoSit.Domain.Bookings;
using GoSit.Domain.Common.ValueObjects;
using GoSit.Domain.Pets;

namespace GoSit.Application.Features.Bookings;

/// <summary>
/// A booking as returned to one participant.
///
/// The household details — exact address, the family's notes, and pet care notes — are
/// withheld from a caregiver until they accept: a request goes to someone who has agreed
/// to nothing, so it carries the area and no more. The family always sees their own. Care
/// needs are never here at all — a caregiver reads those from the recipient endpoint,
/// which checks the booking grants access, so sensitive detail is never bundled into a
/// list response.
/// </summary>
public sealed record BookingResponse(
    Guid Id,
    string Status,
    Guid CaregiverProfileId,
    string CaregiverFirstName,
    string CaregiverLastInitial,

    /// <summary>
    /// Who booked the care. Same shape as the caregiver's name — first name and last
    /// initial — because a booking does not need to disclose a full legal name to
    /// either side, and neither side should learn more about the other than the other
    /// learns about them.
    /// </summary>
    string CareRequesterFirstName,
    string CareRequesterLastInitial,

    Guid CareServiceId,
    string CareServiceCode,
    string CareServiceName,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    /// <summary>
    /// The exact address — whose depends on CareLocation. Null when AddressDisclosed is false.
    /// </summary>
    AddressDto? Address,

    /// <summary>
    /// City, province and postal prefix. Always present, so a caregiver can tell whether a
    /// request is near enough to take without learning where the family lives.
    /// </summary>
    BookingAreaDto Area,

    /// <summary>The family's notes. Withheld from a caregiver until they accept, like the address.</summary>
    string? Instructions,
    decimal HourlyRate,

    /// <summary>"Hourly", or "Nightly" for boarding and in-home pet sitting.</summary>
    string PricingUnit,

    /// <summary>The agreed price per night, for a booking priced by the night.</summary>
    decimal? NightlyRate,

    /// <summary>Nights booked, for a booking priced by the night.</summary>
    int? Nights,

    /// <summary>"RecipientHome", or "CaregiverHome" for boarding — which decides whose address this is.</summary>
    string CareLocation,

    decimal EstimatedTotal,
    decimal? ActualTotal,
    DateTime? CheckedInAtUtc,
    DateTime? CheckedOutAtUtc,

    /// <summary>
    /// Whether the caregiver was at the address when they started and finished.
    ///
    /// A status, never a location — the coordinates were compared against the booking
    /// address and discarded, and this is what survived. Shown to both sides: the family
    /// gets the reassurance and the caregiver gets the record that protects them if the
    /// visit is later disputed.
    ///
    /// "NotProvided" is not a failure and must not be presented as one. It means the
    /// device could not offer a location, which is common and is nobody's fault.
    /// </summary>
    string CheckInVerification,
    string CheckOutVerification,

    /// <summary>True when the platform closed the booking; no one checked out.</summary>
    bool WasAutoCompleted,

    string? CancelledBy,
    bool IsLateCancellation,
    string? ResolutionReason,

    /// <summary>
    /// How staff settled a dispute on this booking, and when.
    ///
    /// Shown to both sides, in the same words. The admin form that collects this note
    /// tells whoever writes it that the family and the caregiver will both read it, and
    /// this is the only place either of them can — the notification that accompanies it
    /// says a decision was made and where to look, because it travels by email and a
    /// dispute is about something that happened in a family's home.
    ///
    /// Giving the two sides different accounts of the same decision would be worse than
    /// giving them nothing, so there is one field and they both read it.
    /// </summary>
    DateTime? DisputeResolvedAtUtc,
    string? DisputeResolutionNotes,

    IReadOnlyList<BookingRecipientSummary> Recipients,

    /// <summary>Pets named on a pet-care booking. Care notes only once disclosed.</summary>
    IReadOnlyList<BookingPetSummary> Pets,

    /// <summary>
    /// Whether this response carries the household details — address, notes, pet care
    /// notes. Always true for the family; for a caregiver, from acceptance on.
    /// </summary>
    bool HouseholdDetailsDisclosed,

    /// <summary>
    /// Whether Address is included. Usually the same as HouseholdDetailsDisclosed; for
    /// boarding the address is the caregiver's, so the rule runs the other way.
    /// </summary>
    bool AddressDisclosed,

    DateTime CreatedAtUtc);

/// <summary>
/// A recipient named on a booking: enough for a caregiver to know who they are caring
/// for, with care needs fetched separately and deliberately.
/// </summary>
public sealed record BookingRecipientSummary(
    Guid CareRecipientId,
    string FirstName,
    int Age,
    string RecipientType,
    bool HasRecordedCareNeeds);

public static class BookingMapping
{
    public static BookingResponse ToResponse(
        this CareBooking booking,
        CareServiceDescriptor service,
        CaregiverDescriptor caregiver,
        ParticipantDescriptor careRequester,
        IReadOnlyList<BookingRecipientSummary> recipients,
        IReadOnlyList<BookingPetSummary> pets,
        bool householdDetailsDisclosed,
        bool addressDisclosed) =>
        new(
            booking.Id,
            booking.Status.ToString(),
            booking.CaregiverProfileId,
            caregiver.FirstName,
            caregiver.LastInitial,
            careRequester.FirstName,
            careRequester.LastInitial,
            booking.CareServiceId,
            service.Code,
            service.Name,
            booking.StartAtUtc,
            booking.EndAtUtc,
            addressDisclosed ? booking.Address.ToDto() : null,
            booking.Address.ToArea(),
            householdDetailsDisclosed ? booking.Instructions : null,
            booking.HourlyRateSnapshot,
            booking.PricingUnit.ToString(),
            booking.NightlyRateSnapshot,
            booking.PricingUnit == Domain.CareServices.PricingUnit.Nightly ? booking.ScheduledNights : null,
            booking.CareLocation.ToString(),
            booking.EstimatedTotal,
            booking.ActualTotal,
            booking.CheckedInAtUtc,
            booking.CheckedOutAtUtc,
            booking.CheckInVerification.ToString(),
            booking.CheckOutVerification.ToString(),
            booking.WasAutoCompleted,
            booking.CancelledBy?.ToString(),
            booking.IsLateCancellation,
            booking.ResolutionReason,
            booking.DisputeResolvedAtUtc,
            booking.DisputeResolutionNotes,
            recipients,
            pets,
            householdDetailsDisclosed,
            addressDisclosed,
            booking.CreatedAtUtc);
}

public sealed record CareServiceDescriptor(string Code, string Name);

public sealed record CaregiverDescriptor(string FirstName, string LastInitial);

/// <summary>A booking participant, named the way both sides are named: first name and
/// last initial.</summary>
public sealed record ParticipantDescriptor(string FirstName, string LastInitial);

/// <summary>Where a booking is, at the resolution a caregiver needs to decide whether to take it.</summary>
public sealed record BookingAreaDto(string City, string Province, string PostalCodePrefix);

/// <summary>
/// A pet named on a booking. Name, species, breed and size are always shown; care notes
/// are null until the household details are disclosed.
/// </summary>
public sealed record BookingPetSummary(
    Guid PetId,
    string Name,
    string Species,
    string? Breed,
    string? Size,
    PetCareNotesResponse? CareNotes);

public sealed record PetCareNotesResponse(
    string? FeedingInstructions,
    string? Medication,
    string? Behaviour,
    string? VetClinic,
    string? OtherNotes);

public static class BookingDisclosureMapping
{
    /// <summary>The forward sortation area only — "M5S", not "M5S 1M2".</summary>
    public static BookingAreaDto ToArea(this Address address)
    {
        var compact = address.PostalCode.Replace(" ", string.Empty);

        return new BookingAreaDto(
            address.City,
            address.Province,
            compact.Length >= 3 ? compact[..3].ToUpperInvariant() : compact.ToUpperInvariant());
    }

    public static PetCareNotesResponse ToCareNotes(this Pet pet) =>
        new(pet.FeedingInstructions, pet.Medication, pet.Behaviour, pet.VetClinic, pet.OtherNotes);
}
