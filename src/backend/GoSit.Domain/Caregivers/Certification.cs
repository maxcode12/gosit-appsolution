using GoSit.Domain.Common;

namespace GoSit.Domain.Caregivers;

/// <summary>
/// Reference data: a kind of certification a caregiver can hold, such as CPR or a
/// vulnerable-sector police check.
///
/// Configurable rather than an enum, because the list grows as GoSit adds senior and
/// specialized-support services — see the taxonomy in CLAUDE.md.
/// </summary>
public sealed class Certification : Entity
{
    private Certification()
    {
    }

    /// <summary>Stable machine key, e.g. "CPR". Used in eligibility rules.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Whether an expiry date must be supplied when a caregiver adds it.</summary>
    public bool RequiresExpiry { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static Certification Create(string code, string name, bool requiresExpiry)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("A certification code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A certification name is required.");
        }

        return new Certification
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            RequiresExpiry = requiresExpiry,
        };
    }
}
