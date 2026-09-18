namespace GoSit.Infrastructure.Persistence;

/// <summary>
/// A provider webhook event that has already been handled.
///
/// Lives in Infrastructure rather than the domain: it carries no business meaning, it
/// exists purely to make at-least-once delivery safe.
/// </summary>
public sealed class ProcessedWebhookEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The provider's own event id. Unique-indexed; this is the dedupe key.</summary>
    public string ProviderEventId { get; init; } = string.Empty;

    public string EventType { get; init; } = string.Empty;

    public DateTime ProcessedAtUtc { get; init; }
}
