using GoSit.Application.Abstractions;
using GoSit.Domain.Interviews;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Scheduling;

public sealed record InterviewRecordingSweepResult(int Deleted);

/// <summary>
/// Deletes interview recordings once they are past their retention period.
///
/// Recordings are kept as evidence for a concern someone raises, and for no other reason.
/// Once the window for that has passed, video of a family's home and a caregiver's face is
/// a liability to both of them and to GoSit, so it goes — unless staff have put the
/// interview on hold for a dispute or incident, in which case nothing is deleted.
///
/// The file is deleted before the row is marked. The other order could leave a record
/// saying a recording is gone while the video still exists.
/// </summary>
public sealed class InterviewRecordingRetentionSweeper(
    IServiceScopeFactory scopeFactory,
    ILogger<InterviewRecordingRetentionSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    private const int BatchSize = 50;

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
                logger.LogError(ex, "The interview recording retention sweep failed and will retry");
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
    public async Task<InterviewRecordingSweepResult> SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var interviews = provider.GetRequiredService<IInterviewRepository>();
        var store = provider.GetRequiredService<IRecordingStore>();
        var audit = provider.GetRequiredService<IAuditTrail>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var utcNow = provider.GetRequiredService<IDateTimeProvider>().UtcNow;

        var due = await interviews.ListWithRecordingsDueForDeletionAsync(utcNow, BatchSize, cancellationToken);
        var deleted = 0;

        foreach (var interview in due)
        {
            foreach (var recording in interview.RecordingsDueForDeletion(utcNow))
            {
                await store.DeleteAsync(recording.StorageKey!, cancellationToken);

                interview.DeleteRecording(recording.Id, utcNow);
                audit.Record(nameof(InterviewRecording), recording.Id, "RecordingDeleted", actorUserId: null);
                deleted++;
            }

            // Saved per interview, so a failure part-way through loses at most one
            // interview's bookkeeping — and the next sweep finds those files already gone.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (deleted > 0)
        {
            logger.LogInformation("Deleted {DeletedCount} interview recordings past retention", deleted);
        }

        return new InterviewRecordingSweepResult(deleted);
    }
}
