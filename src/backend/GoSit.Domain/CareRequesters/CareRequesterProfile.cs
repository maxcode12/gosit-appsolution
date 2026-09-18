using GoSit.Domain.CareRecipients;
using GoSit.Domain.Common.ValueObjects;
using GoSit.Domain.Common;
using GoSit.Domain.Pets;

namespace GoSit.Domain.CareRequesters;

/// <summary>
/// Profile of the person arranging care. Aggregate root for the care recipients and
/// emergency contacts beneath it — nothing outside may add or remove those directly,
/// which is what keeps ownership checks in one place.
/// </summary>
public sealed class CareRequesterProfile : Entity
{
    private const int MaxCareRecipients = 12;
    private const int MaxPets = 10;
    private const int MaxEmergencyContacts = 5;

    private readonly List<CareRecipient> _careRecipients = [];
    private readonly List<Pet> _pets = [];
    private readonly List<EmergencyContact> _emergencyContacts = [];

    private CareRequesterProfile()
    {
    }

    /// <summary>The account this profile belongs to. One profile per user.</summary>
    public Guid UserId { get; private set; }

    public string PhoneNumber { get; private set; } = string.Empty;

    /// <summary>Home address. Exact values are disclosed only on a confirmed booking.</summary>
    public Address? Address { get; private set; }

    public string? AboutMe { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<CareRecipient> CareRecipients => _careRecipients.AsReadOnly();

    public IReadOnlyCollection<Pet> Pets => _pets.AsReadOnly();

    public IReadOnlyCollection<EmergencyContact> EmergencyContacts => _emergencyContacts.AsReadOnly();

    public static CareRequesterProfile Create(Guid userId, string phoneNumber, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A user is required.");
        }

        return new CareRequesterProfile
        {
            UserId = userId,
            PhoneNumber = PhoneNumberRules.Normalize(phoneNumber),
            CreatedAtUtc = utcNow,
        };
    }

    public void UpdateContactDetails(string phoneNumber, string? aboutMe, DateTime utcNow)
    {
        PhoneNumber = PhoneNumberRules.Normalize(phoneNumber);
        AboutMe = string.IsNullOrWhiteSpace(aboutMe) ? null : aboutMe.Trim();
        UpdatedAtUtc = utcNow;
    }

    public void SetAddress(Address address, DateTime utcNow)
    {
        Address = address;
        UpdatedAtUtc = utcNow;
    }

    public CareRecipient AddCareRecipient(
        string firstName,
        string? lastName,
        DateOnly dateOfBirth,
        CareRecipientType recipientType,
        DateTime utcNow)
    {
        if (_careRecipients.Count >= MaxCareRecipients)
        {
            throw new DomainException(
                $"A profile can hold at most {MaxCareRecipients} care recipients.");
        }

        var recipient = CareRecipient.Create(Id, firstName, lastName, dateOfBirth, recipientType, utcNow);
        _careRecipients.Add(recipient);
        UpdatedAtUtc = utcNow;
        return recipient;
    }

    /// <summary>
    /// Looks up a recipient that belongs to this profile. Returning null for a
    /// recipient owned by someone else is deliberate: the caller reports "not found"
    /// rather than "forbidden", so the endpoint cannot confirm that an id exists.
    /// </summary>
    public CareRecipient? FindCareRecipient(Guid careRecipientId) =>
        _careRecipients.SingleOrDefault(r => r.Id == careRecipientId);

    public void UpdateCareRecipient(
        Guid careRecipientId,
        string firstName,
        string? lastName,
        DateOnly dateOfBirth,
        CareRecipientType recipientType,
        DateTime utcNow)
    {
        var recipient = RequireCareRecipient(careRecipientId);
        recipient.UpdateDetails(firstName, lastName, dateOfBirth, recipientType, utcNow);
        UpdatedAtUtc = utcNow;
    }

    public void UpdateCareNeeds(Guid careRecipientId, CareNeedsDetails details, DateTime utcNow)
    {
        var recipient = RequireCareRecipient(careRecipientId);
        recipient.UpdateCareNeeds(details, utcNow);
        UpdatedAtUtc = utcNow;
    }

    public void RemoveCareRecipient(Guid careRecipientId, DateTime utcNow)
    {
        var recipient = RequireCareRecipient(careRecipientId);
        _careRecipients.Remove(recipient);
        UpdatedAtUtc = utcNow;
    }

    public Pet AddPet(PetDetails details, DateTime utcNow)
    {
        if (_pets.Count >= MaxPets)
        {
            throw new DomainException($"A profile can hold at most {MaxPets} pets.");
        }

        var pet = Pet.Create(Id, details, utcNow);
        _pets.Add(pet);
        UpdatedAtUtc = utcNow;
        return pet;
    }

    /// <summary>
    /// Looks up a pet that belongs to this profile. Null for someone else's pet, for the
    /// same reason as <see cref="FindCareRecipient"/>: the endpoint reports "not found"
    /// rather than confirming that an id exists.
    /// </summary>
    public Pet? FindPet(Guid petId) =>
        _pets.SingleOrDefault(p => p.Id == petId);

    public void UpdatePet(Guid petId, PetDetails details, DateTime utcNow)
    {
        var pet = FindPet(petId) ?? throw new DomainException("That pet does not exist.");
        pet.Update(details, utcNow);
        UpdatedAtUtc = utcNow;
    }

    public void RemovePet(Guid petId, DateTime utcNow)
    {
        var pet = FindPet(petId) ?? throw new DomainException("That pet does not exist.");
        _pets.Remove(pet);
        UpdatedAtUtc = utcNow;
    }

    public EmergencyContact AddEmergencyContact(
        string fullName,
        string relationship,
        string phoneNumber,
        DateTime utcNow)
    {
        if (_emergencyContacts.Count >= MaxEmergencyContacts)
        {
            throw new DomainException(
                $"A profile can hold at most {MaxEmergencyContacts} emergency contacts.");
        }

        var contact = EmergencyContact.Create(Id, fullName, relationship, phoneNumber, utcNow);
        _emergencyContacts.Add(contact);
        UpdatedAtUtc = utcNow;
        return contact;
    }

    public void RemoveEmergencyContact(Guid emergencyContactId, DateTime utcNow)
    {
        var contact = _emergencyContacts.SingleOrDefault(c => c.Id == emergencyContactId)
            ?? throw new DomainException("That emergency contact does not exist.");

        _emergencyContacts.Remove(contact);
        UpdatedAtUtc = utcNow;
    }

    private CareRecipient RequireCareRecipient(Guid careRecipientId) =>
        FindCareRecipient(careRecipientId)
        ?? throw new DomainException("That care recipient does not exist.");
}
