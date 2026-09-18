using GoSit.Domain.Common;

namespace GoSit.Domain.Audit;

/// <summary>
/// An immutable record that something consequential happened, written in the same
/// transaction as the change itself.
///
/// Carries identifiers and an action name only. It must never hold care needs,
/// addresses, contact details, or free text copied from a user — an audit trail is
/// read by staff investigating incidents, and must not become another copy of the
/// sensitive data it describes.
/// </summary>
public sealed class AuditEvent : Entity
{
    private AuditEvent()
    {
    }

    /// <summary>Aggregate type, e.g. "CareBooking".</summary>
    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    /// <summary>What happened, e.g. "BookingAccepted".</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>The account that caused it, or null when the platform acted.</summary>
    public Guid? ActorUserId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>Correlation id of the request, for tying an action back to its trace.</summary>
    public string? CorrelationId { get; private set; }

    public static AuditEvent Record(
        string entityType,
        Guid entityId,
        string action,
        Guid? actorUserId,
        DateTime utcNow,
        string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new DomainException("An entity type is required.");
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainException("An action is required.");
        }

        return new AuditEvent
        {
            EntityType = entityType.Trim(),
            EntityId = entityId,
            Action = action.Trim(),
            ActorUserId = actorUserId,
            OccurredAtUtc = utcNow,
            CorrelationId = correlationId,
        };
    }
}
