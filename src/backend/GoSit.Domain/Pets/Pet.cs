using GoSit.Domain.Common;

namespace GoSit.Domain.Pets;

/// <summary>
/// An animal a family arranges care for.
///
/// Deliberately not a care recipient. A care recipient is a person: they carry a date of
/// birth, a care needs profile describing things like toileting and behaviour support, and
/// the disclosure rules that protect children and vulnerable adults. None of that describes
/// a dog, and stretching the person model to fit one would either weaken those rules or
/// bury pet fields among human ones.
///
/// Owned by the care requester's profile, like care recipients, so ownership works the same
/// way: a pet id belonging to someone else is simply not found.
/// </summary>
public sealed class Pet : Entity
{
    public const int MaxNameLength = 60;
    public const int MaxBreedLength = 80;
    public const int MaxNoteLength = 2000;

    private Pet()
    {
    }

    public Guid CareRequesterProfileId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public PetSpecies Species { get; private set; }

    public string? Breed { get; private set; }

    public PetSize? Size { get; private set; }

    /*
     * Care notes. A caregiver sees these only once they have accepted a booking — the same
     * moment they see the exact address — because notes about a pet are where people write
     * "the key is under the mat" and "we are back on Sunday".
     */

    public string? FeedingInstructions { get; private set; }

    public string? Medication { get; private set; }

    /// <summary>Temperament and anything a caregiver should watch for, e.g. reactive to other dogs.</summary>
    public string? Behaviour { get; private set; }

    public string? VetClinic { get; private set; }

    public string? OtherNotes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public bool HasCareNotes =>
        FeedingInstructions is not null
        || Medication is not null
        || Behaviour is not null
        || VetClinic is not null
        || OtherNotes is not null;

    internal static Pet Create(Guid careRequesterProfileId, PetDetails details, DateTime utcNow)
    {
        if (careRequesterProfileId == Guid.Empty)
        {
            throw new DomainException("A care requester is required.");
        }

        var pet = new Pet
        {
            CareRequesterProfileId = careRequesterProfileId,
            CreatedAtUtc = utcNow,
        };

        pet.Apply(details);
        return pet;
    }

    internal void Update(PetDetails details, DateTime utcNow)
    {
        Apply(details);
        UpdatedAtUtc = utcNow;
    }

    private void Apply(PetDetails details)
    {
        var name = details.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A pet needs a name.");
        }

        if (name.Length > MaxNameLength)
        {
            throw new DomainException($"A pet's name must be {MaxNameLength} characters or fewer.");
        }

        if (!Enum.IsDefined(details.Species))
        {
            throw new DomainException("That is not a kind of pet GoSit recognises.");
        }

        if (details.Size is { } size && !Enum.IsDefined(size))
        {
            throw new DomainException("That is not a pet size GoSit recognises.");
        }

        Name = name;
        Species = details.Species;
        Breed = Clean(details.Breed, MaxBreedLength, "A breed");
        Size = details.Size;
        FeedingInstructions = Clean(details.FeedingInstructions, MaxNoteLength, "Feeding instructions");
        Medication = Clean(details.Medication, MaxNoteLength, "Medication notes");
        Behaviour = Clean(details.Behaviour, MaxNoteLength, "Behaviour notes");
        VetClinic = Clean(details.VetClinic, MaxNoteLength, "Vet details");
        OtherNotes = Clean(details.OtherNotes, MaxNoteLength, "Other notes");
    }

    private static string? Clean(string? value, int maxLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{label} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }
}

/// <summary>Everything a family records about a pet, in one value so add and update cannot drift.</summary>
public sealed record PetDetails(
    string Name,
    PetSpecies Species,
    string? Breed,
    PetSize? Size,
    string? FeedingInstructions,
    string? Medication,
    string? Behaviour,
    string? VetClinic,
    string? OtherNotes);
