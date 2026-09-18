namespace GoSit.Application.Abstractions;

/// <summary>
/// Records that something consequential happened, in the same transaction as the change.
///
/// Takes identifiers and an action name only — never care needs, addresses, contact
/// details, or user-supplied free text.
/// </summary>
public interface IAuditTrail
{
    void Record(string entityType, Guid entityId, string action, Guid? actorUserId);
}

/// <summary>The correlation id of the current request, for tying an audit row to a trace.</summary>
public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
