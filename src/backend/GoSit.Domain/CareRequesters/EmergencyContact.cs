using GoSit.Domain.Common;

namespace GoSit.Domain.CareRequesters;

/// <summary>
/// Someone to reach if the CareRequester cannot be contacted during a booking.
/// Contact details are private and released only to a caregiver on a confirmed booking.
/// </summary>
public sealed class EmergencyContact : Entity
{
    private EmergencyContact()
    {
    }

    public Guid CareRequesterProfileId { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public string Relationship { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    internal static EmergencyContact Create(
        Guid careRequesterProfileId,
        string fullName,
        string relationship,
        string phoneNumber,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("An emergency contact name is required.");
        }

        if (string.IsNullOrWhiteSpace(relationship))
        {
            throw new DomainException("A relationship is required.");
        }

        return new EmergencyContact
        {
            CareRequesterProfileId = careRequesterProfileId,
            FullName = fullName.Trim(),
            Relationship = relationship.Trim(),
            PhoneNumber = PhoneNumberRules.Normalize(phoneNumber),
            CreatedAtUtc = utcNow,
        };
    }
}
