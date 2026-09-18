using GoSit.Domain.Common;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Messaging;

/// <summary>
/// Transport for integration events.
///
/// Azure Service Bus is the intended implementation (docs/ARCHITECTURE.md). Until a
/// namespace is provisioned, <see cref="LoggingIntegrationEventPublisher"/> stands in,
/// so the outbox is exercised end to end and only this one class changes later.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}

public sealed class LoggingIntegrationEventPublisher(
    ILogger<LoggingIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        // The type and id only. Payloads carry identifiers, but logging them wholesale
        // would be the wrong habit to build in.
        logger.LogInformation(
            "Integration event {EventType} ({OutboxMessageId}) ready to publish",
            message.Type, message.Id);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Drains the outbox: reads unprocessed messages oldest-first, publishes each, and marks
/// it processed.
///
/// A message is marked processed only after a successful publish, so a crash mid-batch
/// causes a retry rather than a loss. That means at-least-once delivery — consumers must
/// be idempotent, as docs/ARCHITECTURE.md requires.
/// </summary>
public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;

    /// <summary>
    /// After this many failures a message is left alone and logged for investigation,
    /// rather than retried forever at the front of the queue.
    /// </summary>
    private const int MaxAttempts = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a bad batch kill the publisher; the next poll retries.
                logger.LogError(ex, "The outbox publisher failed a cycle and will retry");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task<int> DrainAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

        var pending = await context.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.AttemptCount < MaxAttempts)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        var published = 0;

        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                message.MarkProcessed(DateTime.UtcNow);
                published++;
            }
            catch (Exception ex)
            {
                message.MarkFailed(ex.Message);

                logger.LogWarning(
                    ex,
                    "Failed to publish {EventType} ({OutboxMessageId}), attempt {AttemptCount}",
                    message.Type, message.Id, message.AttemptCount);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return published;
    }
}
