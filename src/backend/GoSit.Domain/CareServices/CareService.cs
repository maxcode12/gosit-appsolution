using GoSit.Domain.CareRecipients;
using GoSit.Domain.Common;
using GoSit.Domain.Pets;

namespace GoSit.Domain.CareServices;

/// <summary>
/// A bookable care service, such as Babysitting or Dementia Companion Care.
///
/// Carries the qualification requirements for the service. The backend evaluates a
/// caregiver against these before showing them in search and again before a booking
/// can be accepted — a caregiver may offer a service without yet being eligible for it.
///
/// All services here are non-medical. Adding a regulated clinical service requires
/// legal and compliance approval; see the medical boundary in CLAUDE.md.
/// </summary>
public sealed class CareService : Entity
{
    private readonly List<string> _requiredCertificationCodes = [];
    private readonly List<CareRecipientType> _applicableRecipientTypes = [];
    private readonly List<PetSpecies> _applicablePetSpecies = [];

    private CareService()
    {
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid CareServiceCategoryId { get; private set; }

    public CareServiceCategory Category { get; private set; } = null!;

    public bool IsActive { get; private set; } = true;

    /// <summary>How this service is priced. Boarding and in-home pet sitting are by the night; everything else by the hour.</summary>
    public PricingUnit PricingUnit { get; private set; } = PricingUnit.Hourly;

    /// <summary>
    /// Where the care happens, which decides whose address a booking carries and who may
    /// see it. Only pet care can happen at the caregiver's home — see <see cref="Create"/>.
    /// </summary>
    public CareLocation CareLocation { get; private set; } = CareLocation.RecipientHome;

    /// <summary>Certification codes a caregiver must validly hold to offer this service.</summary>
    public IReadOnlyList<string> RequiredCertificationCodes => _requiredCertificationCodes.AsReadOnly();

    /// <summary>Which kinds of care recipient this service is appropriate for.</summary>
    public IReadOnlyList<CareRecipientType> ApplicableRecipientTypes =>
        _applicableRecipientTypes.AsReadOnly();

    public bool AppliesTo(CareRecipientType recipientType) =>
        _applicableRecipientTypes.Contains(recipientType);

    /// <summary>
    /// The kinds of animal this service is for. Empty for care for people — a service is
    /// one or the other, never both.
    /// </summary>
    public IReadOnlyList<PetSpecies> ApplicablePetSpecies => _applicablePetSpecies.AsReadOnly();

    /// <summary>
    /// Care for animals rather than people. Decides the screening a caregiver needs and
    /// whether a booking names pets or care recipients.
    /// </summary>
    public bool IsPetCare => _applicablePetSpecies.Count > 0;

    public bool AppliesTo(PetSpecies species) =>
        _applicablePetSpecies.Contains(species);

    public static CareService Create(
        Guid categoryId,
        string code,
        string name,
        string? description,
        IEnumerable<string> requiredCertificationCodes,
        IEnumerable<CareRecipientType> applicableRecipientTypes,
        IEnumerable<PetSpecies>? applicablePetSpecies = null,
        PricingUnit pricingUnit = PricingUnit.Hourly,
        CareLocation careLocation = CareLocation.RecipientHome)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("A service code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A service name is required.");
        }

        var recipientTypes = applicableRecipientTypes.Distinct().ToList();
        var petSpecies = (applicablePetSpecies ?? Enumerable.Empty<PetSpecies>()).Distinct().ToList();

        if (recipientTypes.Count == 0 && petSpecies.Count == 0)
        {
            throw new DomainException("A service must apply to at least one kind of care recipient.");
        }

        // Screening, booking and disclosure all branch on which kind of care this is, so a
        // service claiming both would be screened by whichever rule happened to run first.
        if (recipientTypes.Count > 0 && petSpecies.Count > 0)
        {
            throw new DomainException("A service is care for people or for pets, not both.");
        }

        if (!Enum.IsDefined(pricingUnit) || !Enum.IsDefined(careLocation))
        {
            throw new DomainException("That is not a pricing unit or care location GoSit recognises.");
        }

        // Care for people at a caregiver's own home is the arrangement Ontario licenses as
        // home child care. GoSit does not offer it, and a service must not be able to
        // describe it by accident.
        if (careLocation == CareLocation.CaregiverHome && petSpecies.Count == 0)
        {
            throw new DomainException("Only pet care can happen at the caregiver's home.");
        }

        var service = new CareService
        {
            CareServiceCategoryId = categoryId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            PricingUnit = pricingUnit,
            CareLocation = careLocation,
        };

        service._requiredCertificationCodes.AddRange(
            requiredCertificationCodes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct());

        service._applicableRecipientTypes.AddRange(recipientTypes);
        service._applicablePetSpecies.AddRange(petSpecies);
        return service;
    }
}
