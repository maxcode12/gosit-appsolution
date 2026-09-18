using GoSit.Application.Abstractions;
using GoSit.Infrastructure.Email;
using GoSit.Infrastructure.Payments;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoSit.Infrastructure.Scheduling;

public sealed record NotificationEmailResult(int Sent, int Failed, int Suppressed);

/// <summary>
/// Emails notifications that are waiting to go out.
///
/// Out of band rather than inline, for two reasons. A relay that is slow or down must
/// not make accepting a booking slow or fail — the booking is the thing that matters,
/// and the email is a courtesy. And sending inside the request's transaction would mean
/// either emailing about a booking that then rolled back, or holding a database
/// transaction open across a network call to a third party.
///
/// The rules mirror the payout sweep, for the same reasons:
///
/// - **The attempt is recorded before the send**, so a failure by exception backs off
///   like one by response.
/// - **Failures back off** and eventually give up. Unlike money, a notification email is
///   not owed forever, and the in-app notification is still there regardless.
/// - **"Not sent" is never recorded as "sent".** With no provider configured the
///   notification is marked suppressed, so the record never claims someone was told
///   something they were not.
/// </summary>
public sealed class NotificationEmailDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationEmailDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The notification email dispatch failed and will retry");
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

    /// <summary>Runs one cycle. Public so a test can drive a deterministic dispatch.</summary>
    public async Task<NotificationEmailResult> DispatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var context = provider.GetRequiredService<GoSitDbContext>();
        var sender = provider.GetRequiredService<IEmailSender>();
        var clock = provider.GetRequiredService<IDateTimeProvider>();
        var webApp = provider.GetRequiredService<IOptions<WebAppOptions>>().Value;

        var utcNow = clock.UtcNow;

        // Joined to the account so a notification for a deleted user is not repeatedly
        // picked up and repeatedly found to have nowhere to go.
        var pending = await context.Notifications
            .Where(n => n.EmailSentAtUtc == null && !n.EmailSuppressed)
            .OrderBy(n => n.CreatedAtUtc)
            .Take(BatchSize)
            .Join(
                context.Users,
                notification => notification.UserId,
                user => user.Id,
                (notification, user) => new
                {
                    Notification = notification,
                    user.Email,
                    user.FirstName,
                })
            .ToListAsync(cancellationToken);

        var sent = 0;
        var failed = 0;
        var suppressed = 0;

        foreach (var item in pending)
        {
            var notification = item.Notification;

            if (!notification.IsEmailDue(utcNow))
            {
                continue;
            }

            // No provider: record honestly that nothing was sent, and stop retrying.
            if (!sender.IsLive)
            {
                notification.SuppressEmail();
                suppressed++;
                continue;
            }

            notification.RecordEmailAttempt(utcNow);

            try
            {
                var message = NotificationEmailTemplate.Render(
                    notification, item.FirstName, item.Email, webApp.BaseUrl);

                await sender.SendAsync(message, cancellationToken);

                notification.MarkEmailSent(utcNow);
                sent++;
            }
            catch (Exception ex)
            {
                failed++;

                // The recipient address is personal data and stays out of the log. The
                // notification id is enough to find it.
                logger.LogWarning(
                    ex,
                    "Could not email notification {NotificationId}; it will be retried",
                    notification.Id);
            }
        }

        if (sent > 0 || failed > 0 || suppressed > 0)
        {
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Notification email dispatch sent {Sent}, failed {Failed}, suppressed {Suppressed}",
                sent, failed, suppressed);
        }

        return new NotificationEmailResult(sent, failed, suppressed);
    }
}
