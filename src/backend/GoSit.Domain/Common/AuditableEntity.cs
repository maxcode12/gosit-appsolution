namespace GoSit.Domain.Common;

/// <summary>
/// Entity with creation/modification audit timestamps, stored in UTC.
/// </summary>
public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ModifiedAtUtc { get; set; }
}
