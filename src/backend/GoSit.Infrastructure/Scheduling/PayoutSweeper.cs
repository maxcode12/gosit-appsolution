using GoSit.Application.Abstractions;
using GoSit.Domain.Payouts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Scheduling;

public sealed record PayoutSweepResult(int Sent, int Failed, int Skipped);

/// <summary>
/// Sends money caregivers have earned.
///
/// A payout is created the moment a booking's payment settles, and sits queued until
/// this runs. It is the only thing that moves money out of the platform, so the rules
/// it follows matter:
///
/// - **Only to an account the provider says can receive it.** Sending to an account
///   that is not ready produces a failed transfer and a confused caregiver.
/// - **The attempt is recorded before the provider is called**, so a transfer that fails
///   by exception backs off exactly like one that fails by response. Without that, a
///   provider outage would be retried every cycle forever.
/// - **The idempotency key is the payout id**, so a crash between calling the provider
///   and saving cannot pay someone twice. This is the single most important line in the
///   file: a duplicated transfer is real money gone.
/// - **A failure leaves the payout owed**, not cancelled. The caregiver earned it.
///
/// Marking a payout *paid* is not this sweep's job — that comes from the provider's
/// transfer webhook, the same way a payment is only ever marked succeeded by one.
/// </summary>
public sealed class PayoutSweeper(
    IServiceScopeFactory scopeFactory,
    ILogger<PayoutSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);
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
                // One bad cycle must not kill the sweeper — it is the only thing paying
                // people.
                logger.LogError(ex, "The payout sweep failed and will retry");
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
    public async Task<PayoutSweepResult> SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var payments = provider.GetRequiredService<IPaymentProvider>();
        var payouts = provider.GetRequiredService<IPayoutRepository>();
        var caregivers = provider.GetRequiredService<ICaregiverProfileRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var clock = provider.GetRequiredService<IDateTimeProvider>();

        var utcNow = clock.UtcNow;
        var candidates = await payouts.ListSendableAsync(BatchSize, cancellationToken);

        var sent = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var payout in candidates)
        {
            // Backoff is decided on the aggregate, not here, so the rule lives with the
            // data it governs.
            if (!payout.IsReadyToSend(utcNow))
            {
                skipped++;
                continue;
            }

            var caregiver = await caregivers.FindByIdAsync(
                payout.CaregiverProfileId, cancellationToken);

            if (caregiver is null || !caregiver.CanReceivePayouts)
            {
                skipped++;
                continue;
            }

            // Recorded before the call. See the class remarks.
            payout.RecordAttempt(utcNow);

            try
            {
                var transfer = await payments.CreateTransferAsync(
                    caregiver.PayoutAccountId!,
                    payout.Amount,
                    payout.Currency,
                    // The payout id, so a retry after a crash reuses the provider's
                    // original transfer instead of sending the money twice.
                    $"payout:{payout.Id}",
                    cancellationToken);

                payout.MarkInTransit(transfer.ProviderTransferId);
                sent++;

                logger.LogInformation(
                    "Payout {PayoutId} handed to the provider as {TransferId}",
                    payout.Id,
                    transfer.ProviderTransferId);
            }
            catch (Exception ex)
            {
                // Stays owed and backs off. The amount is never logged alongside the
                // reason — a failure line is not the place for someone's earnings.
                payout.MarkFailed(ex.Message);
                failed++;

                logger.LogWarning(
                    ex, "Payout {PayoutId} could not be sent; it stays owed", payout.Id);
            }

            // Saved per payout rather than per batch: one failure must not roll back the
            // transfers that already succeeded, which would lose the provider references
            // and risk sending them again.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (sent > 0 || failed > 0)
        {
            logger.LogInformation(
                "Payout sweep sent {Sent}, failed {Failed}, skipped {Skipped}",
                sent, failed, skipped);
        }

        return new PayoutSweepResult(sent, failed, skipped);
    }
}
