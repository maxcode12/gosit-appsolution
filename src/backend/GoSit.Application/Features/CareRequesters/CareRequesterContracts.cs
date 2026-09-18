using GoSit.Application.Features.Pets;
using GoSit.Domain.CareRecipients;
using GoSit.Domain.CareRequesters;
using GoSit.Domain.Common.ValueObjects;

namespace GoSit.Application.Features.CareRequesters;

/// <summary>Address as it crosses the API boundary.</summary>
public sealed record AddressDto(
    string Line1,
    string? Line2,
    string City,
    string Province,
    string PostalCode,
    string Country = "CA");

public sealed record CareRequesterProfileResponse(
    Guid Id,
    string PhoneNumber,
    string? AboutMe,
    AddressDto? Address,
    IReadOnlyList<CareRecipientSummary> CareRecipients,
    IReadOnlyList<PetResponse> Pets,
    IReadOnlyList<EmergencyContactResponse> EmergencyContacts);

/// <summary>
/// A care recipient without their care needs. Listing a profile returns summaries;
/// the sensitive detail is fetched deliberately, one recipient at a time.
/// </summary>
public sealed record CareRecipientSummary(
    Guid Id,
    string FirstName,
    string? LastName,
    DateOnly DateOfBirth,
    int Age,
    string RecipientType,
    bool HasRecordedCareNeeds);

public sealed record CareNeedsResponse(
    string? MobilityNeeds,
    string? CommunicationStyle,
    string? SensoryNeeds,
    string? BehaviourSupportNotes,
    string? DietaryRestrictions,
    string? Allergies,
    bool ToiletingAssistance,
    bool FeedingAssistance,
    string? RoutinePreferences,
    string? Triggers,
    string? CalmingStrategies,
    string? EmergencyInstructions,
    IReadOnlyList<string> RequiredSkills,
    DateTime? UpdatedAtUtc);

public sealed record CareRecipientResponse(
    Guid Id,
    string FirstName,
    string? LastName,
    DateOnly DateOfBirth,
    int Age,
    string RecipientType,
    CareNeedsResponse CareNeeds);

public sealed record EmergencyContactResponse(
    Guid Id,
    string FullName,
    string Relationship,
    string PhoneNumber);

/// <summary>
/// Maps domain objects to responses. Kept in one place so a new field cannot be
/// exposed by accident — adding it here is a deliberate act.
/// </summary>
public static class CareRequesterMapping
{
    public static AddressDto ToDto(this Address address) =>
        new(address.Line1, address.Line2, address.City, address.Province, address.PostalCode, address.Country);

    public static Address ToDomain(this AddressDto dto) =>
        Address.Create(dto.Line1, dto.Line2, dto.City, dto.Province, dto.PostalCode, dto.Country);

    public static CareRequesterProfileResponse ToResponse(
        this CareRequesterProfile profile,
        DateOnly today) =>
        new(
            profile.Id,
            profile.PhoneNumber,
            profile.AboutMe,
            profile.Address?.ToDto(),
            profile.CareRecipients.Select(r => r.ToSummary(today)).ToList(),
            profile.Pets.OrderBy(p => p.Name).Select(p => p.ToResponse()).ToList(),
            profile.EmergencyContacts.Select(c => c.ToResponse()).ToList());

    public static CareRecipientSummary ToSummary(this CareRecipient recipient, DateOnly today) =>
        new(
            recipient.Id,
            recipient.FirstName,
            recipient.LastName,
            recipient.DateOfBirth,
            recipient.AgeOn(today),
            recipient.RecipientType.ToString(),
            !recipient.CareNeeds.IsEmpty);

    public static CareRecipientResponse ToResponse(this CareRecipient recipient, DateOnly today) =>
        new(
            recipient.Id,
            recipient.FirstName,
            recipient.LastName,
            recipient.DateOfBirth,
            recipient.AgeOn(today),
            recipient.RecipientType.ToString(),
            recipient.CareNeeds.ToResponse());

    public static CareNeedsResponse ToResponse(this CareNeedsProfile needs) =>
        new(
            needs.MobilityNeeds,
            needs.CommunicationStyle,
            needs.SensoryNeeds,
            needs.BehaviourSupportNotes,
            needs.DietaryRestrictions,
            needs.Allergies,
            needs.ToiletingAssistance,
            needs.FeedingAssistance,
            needs.RoutinePreferences,
            needs.Triggers,
            needs.CalmingStrategies,
            needs.EmergencyInstructions,
            needs.RequiredSkills,
            needs.UpdatedAtUtc);

    public static EmergencyContactResponse ToResponse(this EmergencyContact contact) =>
        new(contact.Id, contact.FullName, contact.Relationship, contact.PhoneNumber);
}
