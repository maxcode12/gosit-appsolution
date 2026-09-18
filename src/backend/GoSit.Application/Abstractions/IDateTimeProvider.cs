namespace GoSit.Application.Abstractions;

/// <summary>Server time, always UTC. Injected so time-dependent rules stay testable.</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
