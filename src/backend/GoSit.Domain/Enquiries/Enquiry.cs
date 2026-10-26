using GoSit.Domain.Common;

namespace GoSit.Domain.Enquiries;

public enum EnquiryKind
{
    Family = 1,
    Caregiver = 2,

    /// <summary>A partner, vendor, funder or anyone writing as an organisation.</summary>
    Partner = 3,
    Other = 4,
}

public enum EnquiryStatus
{
    New = 1,
    Handled = 2,

    /// <summary>Not a real enquiry. Kept rather than deleted, so a pattern of abuse stays visible.</summary>
    Spam = 3,
}

/// <summary>
/// A message sent through the contact form on the public website, by someone who does not
/// have an account.
///
/// The name, address and message are personal information from a member of the public and
/// are never logged. The address is for replying and nothing else: it is not a sign-up and
/// the sender is not added to any list.
/// </summary>
public sealed class Enquiry : Entity
{
    public const int MaxNameLength = 120;
    public const int MaxEmailLength = 256;
    public const int MinMessageLength = 10;
    public const int MaxMessageLength = 2000;
    public const int MaxSourceLength = 50;

    private Enquiry()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public EnquiryKind Kind { get; private set; }

    public string Message { get; private set; } = string.Empty;

    /// <summary>Where the form was submitted from, for example "website".</summary>
    public string Source { get; private set; } = string.Empty;

    public EnquiryStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>When the sender ticked the consent box. A message cannot exist without it.</summary>
    public DateTime ConsentedAtUtc { get; private set; }

    public Guid? HandledByUserId { get; private set; }

    public DateTime? HandledAtUtc { get; private set; }

    public int Version { get; private set; }

    public static Enquiry Submit(
        string name,
        string email,
        EnquiryKind kind,
        string message,
        string source,
        bool consentGiven,
        DateTime utcNow)
    {
        if (!consentGiven)
        {
            throw new DomainException("Consent to be contacted is required.");
        }

        var trimmedName = Required(name, "A name", MaxNameLength);
        var trimmedEmail = Required(email, "An email address", MaxEmailLength);
        var trimmedMessage = Required(message, "A message", MaxMessageLength);

        if (trimmedMessage.Length < MinMessageLength)
        {
            throw new DomainException($"A message must be at least {MinMessageLength} characters.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new DomainException("That kind of enquiry is not recognised.");
        }

        return new Enquiry
        {
            Name = trimmedName,
            Email = trimmedEmail,
            Kind = kind,
            Message = trimmedMessage,
            Source = Required(source, "A source", MaxSourceLength),
            Status = EnquiryStatus.New,
            CreatedAtUtc = utcNow,
            ConsentedAtUtc = utcNow,
        };
    }

    public void MarkHandled(Guid staffUserId, DateTime utcNow) => Close(EnquiryStatus.Handled, staffUserId, utcNow);

    public void MarkSpam(Guid staffUserId, DateTime utcNow) => Close(EnquiryStatus.Spam, staffUserId, utcNow);

    private void Close(EnquiryStatus status, Guid staffUserId, DateTime utcNow)
    {
        if (staffUserId == Guid.Empty)
        {
            throw new DomainException("A staff member is required.");
        }

        if (Status != EnquiryStatus.New)
        {
            throw new DomainException($"This enquiry has already been marked {Status}.");
        }

        Status = status;
        HandledByUserId = staffUserId;
        HandledAtUtc = utcNow;
        Version++;
    }

    private static string Required(string? value, string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{label} is required.");
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{label} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }
}
