using GoSit.Domain.CareRecipients;
using GoSit.Domain.CareServices;
using GoSit.Domain.Pets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Persistence;

/// <summary>
/// Seeds the care-service taxonomy from CLAUDE.md.
///
/// Run at startup rather than through EF's HasData, because CareService carries
/// primitive collections (required certifications, applicable recipient types) that
/// migration seed data cannot express. Idempotent: it inserts what is missing and
/// leaves existing rows alone, so it is safe to run on every boot.
///
/// Every service here is non-medical. Adding a regulated clinical service needs legal
/// and compliance approval first.
/// </summary>
public sealed partial class ReferenceDataSeeder(
    GoSitDbContext context,
    IConfiguration configuration,
    ILogger<ReferenceDataSeeder> logger)
{
    private const string ChildCare = "CHILD_CARE";
    private const string SeniorCare = "SENIOR_CARE";
    private const string SpecializedSupport = "SPECIALIZED_SUPPORT";
    private const string FamilySupport = "FAMILY_SUPPORT";
    private const string PetCare = "PET_CARE";

    private static readonly (string Code, string Name, int Order)[] Categories =
    [
        (ChildCare, "Child Care", 1),
        (SeniorCare, "Senior Care", 2),
        (SpecializedSupport, "Specialized Support", 3),
        (FamilySupport, "Family Support", 4),
        (PetCare, "Pet Care", 5),
    ];

    private static readonly CareRecipientType[] NoRecipients = [];
    private static readonly PetSpecies[] Dogs = [PetSpecies.Dog];
    private static readonly PetSpecies[] AllPets = Enum.GetValues<PetSpecies>();
    private static readonly CareRecipientType[] Child = [CareRecipientType.Child];
    private static readonly CareRecipientType[] Senior = [CareRecipientType.Senior];
    private static readonly CareRecipientType[] AdultAndSenior =
        [CareRecipientType.Adult, CareRecipientType.Senior];
    private static readonly CareRecipientType[] AllRecipients =
        [CareRecipientType.Child, CareRecipientType.Adult, CareRecipientType.Senior];

    private sealed record ServiceDefinition(
        string CategoryCode,
        string Code,
        string Name,
        string[] RequiredCertifications,
        CareRecipientType[] RecipientTypes,
        PetSpecies[]? PetSpecies = null,
        PricingUnit Pricing = PricingUnit.Hourly,
        CareLocation Location = CareLocation.RecipientHome);

    /// <summary>
    /// Certification requirements are a starting position, not a legal determination.
    /// They need review by whoever owns Trust and Safety policy before launch —
    /// see the open question recorded in docs/PLAN.md.
    /// </summary>
    private static readonly ServiceDefinition[] Services =
    [
        // Child Care
        new(ChildCare, "BABYSITTING", "Babysitting", ["VULNERABLE_SECTOR"], Child),
        new(ChildCare, "NANNY_CARE", "Nanny Care", ["VULNERABLE_SECTOR", "FIRST_AID"], Child),
        new(ChildCare, "AFTER_SCHOOL_CARE", "After-School Care", ["VULNERABLE_SECTOR"], Child),
        new(ChildCare, "OVERNIGHT_CHILD_CARE", "Overnight Child Care",
            ["VULNERABLE_SECTOR", "FIRST_AID", "CPR"], Child),

        // Senior Care
        new(SeniorCare, "SENIOR_COMPANIONSHIP", "Senior Companionship", ["POLICE_CHECK"], Senior),
        new(SeniorCare, "MEAL_SUPPORT", "Meal Support", ["POLICE_CHECK"], AdultAndSenior),
        new(SeniorCare, "ERRAND_ASSISTANCE", "Errand Assistance", ["POLICE_CHECK"], AdultAndSenior),
        new(SeniorCare, "MOBILITY_ASSISTANCE", "Mobility Assistance",
            ["POLICE_CHECK", "MOBILITY_SUPPORT"], AdultAndSenior),
        new(SeniorCare, "APPOINTMENT_COMPANION", "Appointment Companion", ["POLICE_CHECK"], AdultAndSenior),
        new(SeniorCare, "DEMENTIA_COMPANION_CARE", "Dementia Companion Care",
            ["POLICE_CHECK", "DEMENTIA_CARE"], Senior),

        // Specialized Support
        new(SpecializedSupport, "AUTISM_SUPPORT", "Autism Support",
            ["VULNERABLE_SECTOR", "AUTISM_SUPPORT"], AllRecipients),
        new(SpecializedSupport, "DEVELOPMENTAL_DISABILITY_SUPPORT", "Developmental Disability Support",
            ["VULNERABLE_SECTOR"], AllRecipients),
        new(SpecializedSupport, "SPECIAL_NEEDS_RESPITE", "Special-Needs Respite",
            ["VULNERABLE_SECTOR", "FIRST_AID"], AllRecipients),
        new(SpecializedSupport, "COMMUNICATION_SUPPORT", "Communication Support",
            ["VULNERABLE_SECTOR"], AllRecipients),
        new(SpecializedSupport, "MOBILITY_SUPPORT_SERVICE", "Mobility Support",
            ["VULNERABLE_SECTOR", "MOBILITY_SUPPORT"], AllRecipients),
        new(SpecializedSupport, "BEHAVIOURAL_SUPPORT", "Behavioural Support",
            ["VULNERABLE_SECTOR", "BEHAVIOUR_SUPPORT"], AllRecipients),

        // Family Support
        new(FamilySupport, "RESPITE_CARE", "Respite Care", ["VULNERABLE_SECTOR"], AllRecipients),
        new(FamilySupport, "TRANSPORTATION_ASSISTANCE", "Transportation Assistance",
            ["POLICE_CHECK"], AllRecipients),
        new(FamilySupport, "HOUSEHOLD_SUPPORT", "Care-Related Light Household Support",
            ["POLICE_CHECK"], AllRecipients),
        new(FamilySupport, "COMPANIONSHIP_SUPPORT", "Routine and Companionship Support",
            ["POLICE_CHECK"], AllRecipients),

        // Pet Care. No certificates: screening comes from the caregiver's clearance — photo
        // ID and a criminal record check — see VerificationRequirements.
        //
        new(PetCare, "DOG_WALKING", "Dog Walking", [], NoRecipients, Dogs),
        new(PetCare, "PET_DROP_IN_VISITS", "Drop-In Pet Visits", [], NoRecipients, AllPets),
        // A sitter staying over at the family's home: priced per night, like boarding, but
        // at the family's address. Short evening sits are drop-in visits, by the hour.
        new(PetCare, "IN_HOME_PET_SITTING", "In-Home Pet Sitting", [], NoRecipients, AllPets,
            PricingUnit.Nightly),

        // Boarding is at the caregiver's home and priced per night. It is the one service
        // whose booking carries the caregiver's address, shown to the family once the
        // caregiver accepts — see CareBooking.AddressDisclosedTo.
        new(PetCare, "PET_BOARDING", "Pet Boarding", [], NoRecipients, AllPets,
            PricingUnit.Nightly, CareLocation.CaregiverHome),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var categoriesByCode = await SeedCategoriesAsync(cancellationToken);
            await SeedServicesAsync(categoriesByCode, cancellationToken);
            await SeedSubscriptionPlansAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Two instances booting together can both see a row as missing and both try
            // to insert it; the unique index on Code means one loses. The end state is
            // still correct, so this is logged rather than allowed to fail startup.
            logger.LogInformation(
                ex, "Reference data seeding raced another instance; the data is already present");

            context.ChangeTracker.Clear();
        }
    }

    private async Task<Dictionary<string, Guid>> SeedCategoriesAsync(CancellationToken cancellationToken)
    {
        var existing = await context.CareServiceCategories
            .ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);

        var added = 0;

        foreach (var (code, name, order) in Categories)
        {
            if (existing.ContainsKey(code))
            {
                continue;
            }

            var category = CareServiceCategory.Create(code, name, order);
            context.CareServiceCategories.Add(category);
            existing[code] = category.Id;
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} care service categories", added);
        }

        return existing;
    }

    private async Task SeedServicesAsync(
        IReadOnlyDictionary<string, Guid> categoriesByCode,
        CancellationToken cancellationToken)
    {
        var existingCodes = await context.CareServices
            .Select(s => s.Code)
            .ToListAsync(cancellationToken);

        var known = existingCodes.ToHashSet();
        var added = 0;

        foreach (var definition in Services)
        {
            if (known.Contains(definition.Code))
            {
                continue;
            }

            if (!categoriesByCode.TryGetValue(definition.CategoryCode, out var categoryId))
            {
                logger.LogWarning(
                    "Skipping service {ServiceCode}: category {CategoryCode} is missing",
                    definition.Code, definition.CategoryCode);
                continue;
            }

            context.CareServices.Add(CareService.Create(
                categoryId,
                definition.Code,
                definition.Name,
                description: null,
                definition.RequiredCertifications,
                definition.RecipientTypes,
                definition.PetSpecies,
                definition.Pricing,
                definition.Location));

            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} care services", added);
        }
    }
}
