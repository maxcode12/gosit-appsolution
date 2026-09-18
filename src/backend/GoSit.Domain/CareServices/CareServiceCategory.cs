using GoSit.Domain.Common;

namespace GoSit.Domain.CareServices;

/// <summary>
/// Top-level grouping of care services — Child Care, Senior Care, Specialized Support,
/// Family Support. Reference data, seeded and configurable, because the taxonomy grows
/// as GoSit adds verticals.
/// </summary>
public sealed class CareServiceCategory : Entity
{
    private CareServiceCategory()
    {
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static CareServiceCategory Create(string code, string name, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("A category code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A category name is required.");
        }

        return new CareServiceCategory
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            DisplayOrder = displayOrder,
        };
    }
}
