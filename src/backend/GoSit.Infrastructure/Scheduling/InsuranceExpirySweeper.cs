using GoSit.Application.Abstractions;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Scheduling;

public sealed record InsuranceExpirySweepResult(int Warned, int Lapsed);

/// <summary>
/// Warns caregivers about insurance policies that are about to lapse, and tells them
/// when one has. Mirrors <see cref="CertificationExpirySweeper"/> exactly — same 30-day
/// and lapsed staging, same reasoning: a caregiver whose coverage quietly expires should
/// not find out only because work stops arriving.
///
/// This does not yet remove anyone's eligibility for a booking — see
/// docs/TRUST_AND_SAFETY.md and <see cref="CaregiverProfile.HasValidInsurance"/> for why
/// that is still an open decision, not a built rule.
/// </summary>
public sealed class InsuranceExpirySweeper(
    IServiceScopeFactory scopeFactory,
    ILogger<InsuranceExpirySweeper> logger) : BackgroundService
{
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
                logger.LogError(ex, "The insurance expiry sweep failed and will retry");
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
    public async Task<InsuranceExpirySweepResult> SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var review = provider.GetRequiredService<IInsuranceReviewRepository>();
        var profiles = provider.GetRequiredService<ICaregiverProfileRepository>();
        var notifications = provider.GetRequiredService<INotificationRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var clock = provider.GetRequiredService<IDateTimeProvider>();

        var utcNow = clock.UtcNow;
        var today = DateOnly.FromDateTime(utcNow);

        var profileIds = await review.ListProfileIdsWithLapsingInsuranceAsync(
            today, DaysAhead, BatchSize, cancellationToken);

        if (profileIds.Count == 0)
        {
            return new InsuranceExpirySweepResult(0, 0);
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

            foreach (var notice in profile.TakeInsuranceExpiryNotices(today, DaysAhead))
            {
                notifications.Add(Notification.Create(
                    profile.UserId,
                    NotificationType.InsuranceExpiring,
                    Title(notice),
                    Body(notice),
                    nameof(CaregiverInsurancePolicy),
                    notice.CaregiverInsurancePolicyId,
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
            logger.LogInformation(
                "Insurance expiry sweep warned {Warned} and reported {Lapsed} lapsed", warned, lapsed);
        }

        return new InsuranceExpirySweepResult(warned, lapsed);
    }

    private static string Title(LapsingInsurancePolicy notice) =>
        notice.Stage == ExpiryNoticeStage.Expired
            ? $"Your {CoverageName(notice.CoverageType)} insurance has expired"
            : $"Your {CoverageName(notice.CoverageType)} insurance expires soon";

    private static string Body(LapsingInsurancePolicy notice)
    {
        var when = notice.ExpiresOn.ToString("d MMMM yyyy");

        return notice.Stage == ExpiryNoticeStage.Expired
            ? $"It expired on {when}. Upload a current policy to be verified again."
            : $"It expires on {when}. Renew it and upload the new policy before then.";
    }

    private static string CoverageName(InsuranceCoverageType type) => type switch
    {
        InsuranceCoverageType.GeneralLiability => "general liability",
        InsuranceCoverageType.ProfessionalLiability => "professional liability",
        InsuranceCoverageType.Automobile => "auto",
        _ => "",
    };
}
