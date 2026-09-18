namespace GoSit.Domain.Common;

/// <summary>
/// A domain event captured for publication, written in the same database transaction as
/// the state change that produced it.
///
/// This is the outbox pattern from docs/ARCHITECTURE.md: business state and the intent
/// to publish commit together, so a booking can never be accepted without its event
/// eventually reaching consumers, and an event can never describe a change that was
/// rolled back.
/// </summary>
public sealed class OutboxMessage : Entity
{
    private OutboxMessage()
    {
    }

    /// <summary>Event type name, used by consumers to route the payload.</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>JSON payload. Identifiers only — never personal data.</summary>
    public string Payload { get; private set; } = string.Empty;

    public DateTime OccurredAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    /// <summary>Last failure, kept so a stuck message can be diagnosed.</summary>
    public string? LastError { get; private set; }

    public static OutboxMessage Create(string type, string payload, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new DomainException("An event type is required.");
        }

        return new OutboxMessage
        {
            Type = type.Trim(),
            Payload = payload,
            OccurredAtUtc = utcNow,
        };
    }

    public void MarkProcessed(DateTime utcNow)
    {
        ProcessedAtUtc = utcNow;
        AttemptCount++;
        LastError = null;
    }

    /// <summary>
    /// Records a failed attempt. The message stays unprocessed so it is retried; a
    /// consumer that has already seen it must be idempotent.
    /// </summary>
    public void MarkFailed(string error)
    {
        AttemptCount++;
        LastError = error.Length > 1000 ? error[..1000] : error;
    }
}
