using GoSit.Application.Abstractions;
using GoSit.Application.Features.Reviews;
using GoSit.Domain.Reviews;
using GoSit.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Scheduling;

public sealed record ReviewSweepResult(int Published);

/// <summary>
/// Publishes reviews whose window has closed.
///
/// Reviews are written blind and normally publish when both sides have submitted. This
/// covers the case where only one side ever did: without it, a single review would stay
/// hidden forever, which would let silence suppress feedback.
/// </summary>
public sealed class ReviewPublicationSweeper(
    IServiceScopeFactory scopeFactory,
    ILogger<ReviewPublicationSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);
    private const int BatchSize = 100;

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
                logger.LogError(ex, "The review publication sweep failed and will retry");
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

    /// <summary>
    /// Runs one cycle. Public so a test can drive a single deterministic sweep rather
    /// than waiting for the poll interval.
    /// </summary>
    public async Task<ReviewSweepResult> SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var reviews = provider.GetRequiredService<IReviewRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var clock = provider.GetRequiredService<IDateTimeProvider>();
        var ratings = provider.GetRequiredService<RatingRecalculator>();

        var utcNow = clock.UtcNow;

        var pairs = await reviews.ListAwaitingPublicationAsync(
            utcNow - ReviewPolicy.Window, BatchSize, cancellationToken);

        if (pairs.Count == 0)
        {
            return new ReviewSweepResult(0);
        }

        var published = 0;

        foreach (var pair in pairs)
        {
            if (pair.PublishIfWindowClosed(utcNow))
            {
                published++;
            }
        }

        if (published == 0)
        {
            return new ReviewSweepResult(0);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Ratings count published reviews only, so they are recalculated after the
        // publication commits.
        foreach (var pair in pairs)
        {
            await ratings.RecalculateAsync(pair, utcNow, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Published {Count} review pairs past their window", published);

        return new ReviewSweepResult(published);
    }
}
