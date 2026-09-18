using GoSit.Application.Abstractions;
using GoSit.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Scheduling;

public sealed record SubscriptionSweepResult(int Expired);

/// <summary>
/// Closes out subscriptions whose paid period has run out, or which have been past due
/// beyond their grace.
///
/// Entitlement checks already re-evaluate against the clock, so a lapsed row never
/// grants anything even before this runs. The sweep exists to make stored state match
/// reality, so reporting and the caregiver's own view are not quietly wrong.
/// </summary>
public sealed class SubscriptionSweeper(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);
    private const int BatchSize = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // One bad cycle must not kill the sweeper.
                logger.LogError(ex, "The subscription sweep failed and will retry");
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

    /// <summary>Runs one cycle. Public so a test can drive a deterministic sweep.</summary>
    public async Task<SubscriptionSweepResult> SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var subscriptions = provider.GetRequiredService<ISubscriptionRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var clock = provider.GetRequiredService<IDateTimeProvider>();

        var utcNow = clock.UtcNow;

        var due = await subscriptions.ListDueForExpiryAsync(utcNow, BatchSize, cancellationToken);

        var expired = due.Count(subscription => subscription.ExpireIfDue(utcNow));

        if (expired == 0)
        {
            return new SubscriptionSweepResult(0);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Closed out {Count} subscriptions", expired);

        return new SubscriptionSweepResult(expired);
    }
}
