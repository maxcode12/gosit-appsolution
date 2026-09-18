using System.Net.Http.Json;
using GoSit.Domain.Caregivers;
using GoSit.Infrastructure.Persistence;
using GoSit.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Warning a caregiver before a lapsed certificate costs them work.
///
/// Expiry was always enforced and never announced. A caregiver's First Aid certificate ran
/// out, they dropped out of search for every service requiring it, and the only sign was
/// work that stopped arriving. `NotificationType.CertificationExpiring` had been declared
/// since Phase 5 and was raised by nothing.
/// </summary>
[Collection(nameof(ApiCollection))]
public class CertificationExpiryTests(GoSitApiFactory factory)
{
    private sealed record NotificationBody(
        string Type, string Title, string? Body, string? RelatedEntityType);

    private sealed record NotificationListBody(
        IReadOnlyList<NotificationBody> Notifications, int UnreadCount);

    private Task<CertificationExpirySweepResult> SweepAsync() =>
        factory.Services.GetRequiredService<CertificationExpirySweeper>()
            .SweepAsync(CancellationToken.None);

    private static async Task<IReadOnlyList<NotificationBody>> NotificationsAsync(
        HttpClient client)
    {
        var list = await client.GetFromJsonAsync<NotificationListBody>("/api/v1/notifications");
        return list!.Notifications;
    }

    /// <summary>
    /// Moves a caregiver's certificates to a given expiry date. The fixture issues them
    /// with a 2028 expiry, and the domain refuses to accept one already in the past, so
    /// the only honest way to reach these states is to age the record.
    /// </summary>
    private async Task ExpireOnAsync(Guid caregiverProfileId, DateOnly expiresOn)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        await context.Set<CaregiverCertification>()
            .Where(c => c.CaregiverProfileId == caregiverProfileId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.ExpiresOn, expiresOn),
                CancellationToken.None);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task A_caregiver_is_warned_before_a_certificate_lapses()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        await ExpireOnAsync(caregiverId, Today.AddDays(10));

        await SweepAsync();

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var warning = Assert.Single(
            await NotificationsAsync(caregiver), n => n.Type == "CertificationExpiring");

        Assert.Equal("CaregiverCertification", warning.RelatedEntityType);

        // Named and dated. A notice that withheld which certificate and when would fail
        // at the only job it has.
        Assert.Contains("Vulnerable Sector Check", warning.Title);
        Assert.False(string.IsNullOrWhiteSpace(warning.Body));
    }

    /// <summary>
    /// The reason the stage is stored on the certification. The sweeper runs four times a
    /// day, and a caregiver warned every six hours for a month stops reading any of it.
    /// </summary>
    [Fact]
    public async Task Sweeping_repeatedly_does_not_warn_repeatedly()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        await ExpireOnAsync(caregiverId, Today.AddDays(10));

        await SweepAsync();
        await SweepAsync();
        await SweepAsync();

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        Assert.Single(
            await NotificationsAsync(caregiver), n => n.Type == "CertificationExpiring");
    }

    /// <summary>
    /// The second notice is not a repeat of the first. It is the one that answers the
    /// question the caregiver actually has, which is why the work stopped.
    /// </summary>
    [Fact]
    public async Task A_caregiver_is_told_again_when_the_certificate_actually_lapses()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        await ExpireOnAsync(caregiverId, Today.AddDays(10));

        await SweepAsync();

        await ExpireOnAsync(caregiverId, Today.AddDays(-1));
        await SweepAsync();

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var notices = (await NotificationsAsync(caregiver))
            .Where(n => n.Type == "CertificationExpiring")
            .ToList();

        Assert.Equal(2, notices.Count);
        Assert.Contains(notices, n => n.Title.Contains("has expired"));
        Assert.Contains(notices, n => n.Title.Contains("expires soon"));
    }

    [Fact]
    public async Task A_certificate_with_plenty_of_time_left_is_left_alone()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        await ExpireOnAsync(caregiverId, Today.AddDays(200));

        await SweepAsync();

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        Assert.DoesNotContain(
            await NotificationsAsync(caregiver), n => n.Type == "CertificationExpiring");
    }

    /// <summary>
    /// The half of the notice worth reading. A caregiver deciding whether to spend a
    /// Saturday on a refresher course is deciding about the work they would lose.
    /// </summary>
    [Fact]
    public async Task The_warning_names_the_work_the_caregiver_would_lose()
    {
        // Babysitting requires the vulnerable sector check this caregiver holds.
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        await ExpireOnAsync(caregiverId, Today.AddDays(10));

        await SweepAsync();

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var warning = Assert.Single(
            await NotificationsAsync(caregiver), n => n.Type == "CertificationExpiring");

        Assert.Contains("Babysitting", warning.Body ?? "");
    }
}
