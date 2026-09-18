using GoSit.Domain.Common;

namespace GoSit.Domain.CareRecipients;

/// <summary>
/// A person receiving care — a child, an adult, or a senior. Belongs to exactly one
/// CareRequesterProfile, which is the only account permitted to read or change it.
/// </summary>
public sealed class CareRecipient : Entity
{
    private CareRecipient()
    {
    }

    public Guid CareRequesterProfileId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    /// <summary>Optional: many requesters record only a first name for a child.</summary>
    public string? LastName { get; private set; }

    public DateOnly DateOfBirth { get; private set; }

    public CareRecipientType RecipientType { get; private set; }

    public CareNeedsProfile CareNeeds { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public int AgeOn(DateOnly date)
    {
        var age = date.Year - DateOfBirth.Year;
        return DateOfBirth > date.AddYears(-age) ? age - 1 : age;
    }

    internal static CareRecipient Create(
        Guid careRequesterProfileId,
        string firstName,
        string? lastName,
        DateOnly dateOfBirth,
        CareRecipientType recipientType,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new DomainException("A first name is required.");
        }

        var today = DateOnly.FromDateTime(utcNow);

        if (dateOfBirth > today)
        {
            throw new DomainException("A date of birth cannot be in the future.");
        }

        if (dateOfBirth < today.AddYears(-120))
        {
            throw new DomainException("That date of birth is not plausible.");
        }

        var recipient = new CareRecipient
        {
            CareRequesterProfileId = careRequesterProfileId,
            FirstName = firstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim(),
            DateOfBirth = dateOfBirth,
            RecipientType = recipientType,
            CareNeeds = CareNeedsProfile.Empty(),
            CreatedAtUtc = utcNow,
        };

        recipient.EnsureTypeMatchesAge(today);
        return recipient;
    }

    internal void UpdateDetails(
        string firstName,
        string? lastName,
        DateOnly dateOfBirth,
        CareRecipientType recipientType,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new DomainException("A first name is required.");
        }

        var today = DateOnly.FromDateTime(utcNow);

        if (dateOfBirth > today)
        {
            throw new DomainException("A date of birth cannot be in the future.");
        }

        FirstName = firstName.Trim();
        LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim();
        DateOfBirth = dateOfBirth;
        RecipientType = recipientType;
        UpdatedAtUtc = utcNow;

        EnsureTypeMatchesAge(today);
    }

    internal void UpdateCareNeeds(CareNeedsDetails details, DateTime utcNow)
    {
        CareNeeds.Update(details, utcNow);
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Guards the pairing of age and recipient type. Caregiver eligibility and
    /// service matching both key off the type, so an 8-year-old recorded as a Senior
    /// would surface the wrong caregivers.
    /// </summary>
    private void EnsureTypeMatchesAge(DateOnly today)
    {
        var age = AgeOn(today);

        var expected = age switch
        {
            < 18 => CareRecipientType.Child,
            >= 65 => CareRecipientType.Senior,
            _ => CareRecipientType.Adult,
        };

        // Adult and Senior are allowed to overlap: someone aged 60 needing senior care
        // is a legitimate choice by the requester. A minor is not open to interpretation.
        var permitted = expected switch
        {
            CareRecipientType.Child => RecipientType == CareRecipientType.Child,
            _ => RecipientType is CareRecipientType.Adult or CareRecipientType.Senior,
        };

        if (!permitted)
        {
            throw new DomainException(
                $"A care recipient aged {age} cannot be recorded as {RecipientType}.");
        }
    }
}
