namespace GoSit.Domain.Common;

/// <summary>
/// Marker for domain events raised by aggregates. Timestamps are UTC.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}
