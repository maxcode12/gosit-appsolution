using GoSit.Application.Abstractions;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Scheduling;

public sealed record CertificationExpirySweepResult(int Warned, int Lapsed);

/// <summary>
/// Warns caregivers about certifications that are about to lapse, and tells them when one
/// has.
///
/// <see cref="NotificationType.CertificationExpiring"/> was declared in Phase 5 and never
/// raised. Meanwhile expiry is enforced: a lapsed certificate drops the caregiver out of
/// search for every service requiring it and stops them accepting a booking — see
/// <see cref="CaregiverProfile.IsEligibleFor"/>. So the whole thing happened in silence.
/// A caregiver's First Aid certificate ran out, their work quietly stopped arriving, and
/// nothing on the platform connected the two for them. That is somebody's income.
///
/// Two notices: one while there is still time to renew, one when it has gone. The second
/// is not redundant — it is the one that explains why the work stopped.
/// </summary>
public sealed class CertificationExpirySweeper(
    IServiceScopeFactory scopeFactory,
    ILogger<CertificationExpirySweeper> logger) : BackgroundService
{
    /// <summary>
    /// Long enough to book a First Aid course and sit it. A week would technically be a
    /// warning and practically be a notification that the work is already lost.
    /// </summary>
    public const int DaysAhead = 30;

    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(6);
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
                logger.LogError(ex, "The certification expiry sweep failed and will retry");
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
    public async Task<CertificationExpirySweepResult> SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var review = provider.GetRequiredService<ICertificationReviewRepository>();
        var profiles = provider.GetRequiredService<ICaregiverProfileRepository>();
        var notifications = provider.GetRequiredService<INotificationRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var clock = provider.GetRequiredService<IDateTimeProvider>();

        var utcNow = clock.UtcNow;
        var today = DateOnly.FromDateTime(utcNow);

        var profileIds = await review.ListProfileIdsWithLapsingCertificationsAsync(
            today, DaysAhead, BatchSize, cancellationToken);

        if (profileIds.Count == 0)
        {
            return new CertificationExpirySweepResult(0, 0);
        }

        var warned = 0;
        var lapsed = 0;

        foreach (var profileId in profileIds)
        {
            var profile = await profiles.FindByIdAsync(profileId, cancellationToken);

            if (profile is null)
            {
                continue;
            }

            // The aggregate decides what is owed and records that it was said, so a
            // sweeper running four times a day cannot warn the same person repeatedly.
            foreach (var notice in profile.TakeExpiryNotices(today, DaysAhead))
            {
                notifications.Add(Notification.Create(
                    profile.UserId,
                    NotificationType.CertificationExpiring,
                    Title(notice),
                    Body(notice),
                    nameof(CaregiverCertification),
                    notice.CaregiverCertificationId,
                    utcNow));

                if (notice.Stage == ExpiryNoticeStage.Expired)
                {
                    lapsed++;
                }
                else
                {
                    warned++;
                }
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (warned + lapsed > 0)
        {
            // Counts only. Which caregiver holds which lapsing certificate is not
            // something to leave in a log.
            logger.LogInformation(
                "Certification expiry sweep warned {Warned} and reported {Lapsed} lapsed",
                warned,
                lapsed);
        }

        return new CertificationExpirySweepResult(warned, lapsed);
    }

    /*
     * These name the certificate and the date, which is a deliberate departure from the
     * summary-only rule the booking notifications follow.
     *
     * That rule exists to keep other people's business out of a mailbox — a family's
     * message, a care need, a dispute. This is the caregiver's own professional
     * credential and their own deadline, sent to them alone, and a notice that withheld
     * which certificate and when would fail at the only job it has.
     */
    private static string Title(LapsingCertification notice) =>
        notice.Stage == ExpiryNoticeStage.Expired
            ? $"Your {notice.CertificationName} has expired"
            : $"Your {notice.CertificationName} expires soon";

    private static string Body(LapsingCertification notice)
    {
        var when = notice.ExpiresOn.ToString("d MMMM yyyy");

        // What it costs, named. A caregiver deciding whether to spend a Saturday on a
        // refresher course is deciding about the second sentence, not the first.
        var cost = notice.AffectedServiceNames.Count == 0
            ? null
            : $"It is required for {Join(notice.AffectedServiceNames)}.";

        return notice.Stage == ExpiryNoticeStage.Expired
            ? string.Join(" ", new[]
            {
                $"It expired on {when}, so families can no longer book you for the work it covers.",
                cost,
                "Upload a current certificate to start appearing in search again.",
            }.Where(part => part is not null))
            : string.Join(" ", new[]
            {
                $"It expires on {when}.",
                cost,
                "Renew it before then and families will keep being able to book you.",
            }.Where(part => part is not null));
    }

    private static string Join(IReadOnlyList<string> names) =>
        names.Count == 1
            ? names[0]
            : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
}
