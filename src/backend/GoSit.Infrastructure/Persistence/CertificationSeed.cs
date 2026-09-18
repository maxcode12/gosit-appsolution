namespace GoSit.Infrastructure.Persistence;

/// <summary>
/// The certification types GoSit recognizes at launch, seeded as reference data
/// rather than hard-coded as an enum, because the list grows as senior and
/// specialized-support services are added.
///
/// Ids are fixed constants so the seed is idempotent across environments and a
/// caregiver's certification never points at a different type after a re-seed.
/// </summary>
public static class CertificationSeed
{
    public sealed record Entry(Guid Id, string Code, string Name, bool RequiresExpiry);

    public static readonly IReadOnlyList<Entry> Entries =
    [
        new(new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000001"), "CPR", "CPR Certification", true),
        new(new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000002"), "FIRST_AID", "Standard First Aid", true),
        new(new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000003"), "POLICE_CHECK", "Police Record Check", true),
        new(new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000004"), "VULNERABLE_SECTOR", "Vulnerable Sector Check", true),
        new(new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000005"), "ECE", "Early Childhood Education", false),
        new(new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000006"), "PSW", "Personal Support Worker", false),
        new(new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000007"), "DEMENTIA_CARE", "Dementia Care Training", false),
        new(new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000008"), "AUTISM_SUPPORT", "Autism Support Training", false),
        new(new Guid("0f8f2f4a-1c1e-4f1a-9a01-000000000009"), "MOBILITY_SUPPORT", "Safe Transfer and Mobility", false),
        new(new Guid("0f8f2f4a-1c1e-4f1a-9a01-00000000000a"), "BEHAVIOUR_SUPPORT", "Behavioural Support Training", false),
    ];
}
