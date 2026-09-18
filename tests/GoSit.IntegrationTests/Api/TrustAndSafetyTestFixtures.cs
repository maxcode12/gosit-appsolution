using System.Net.Http.Headers;
using System.Net.Http.Json;
using GoSit.Domain.Users;
using GoSit.Infrastructure.Persistence;
using GoSit.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GoSit.IntegrationTests.Api;

public static class TrustAndSafetyTestFixtures
{
    /// <summary>
    /// An authenticated admin client.
    ///
    /// Admin accounts cannot be self-registered — that is enforced in the domain — so the
    /// test promotes a freshly registered account directly in the database. That mirrors
    /// how staff are actually provisioned: out of band, never through the public API.
    /// </summary>
    public static async Task<HttpClient> RegisterAdminAsync(this GoSitApiFactory factory)
    {
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        var client = factory.CreateClient();

        var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "correct-horse-battery-staple",
            firstName = "Sam",
            lastName = "Admin",
            role = "CareRequester",
        });

        registered.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

            await context.Users
                .Where(u => u.Email == email)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Admin));
        }

        // Re-authenticate so the token carries the Admin role claim.
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "correct-horse-battery-staple",
        });

        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<ProfileTestClient.Tokens>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        return client;
    }

    /// <summary>The account id behind a caregiver profile, for reporting and suspension.</summary>
    public static async Task<Guid> UserIdForCaregiverAsync(
        this GoSitApiFactory factory,
        Guid caregiverProfileId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.CaregiverProfiles
            .Where(p => p.Id == caregiverProfileId)
            .Select(p => p.UserId)
            .SingleAsync();
    }

    /// <summary>
    /// Backdates a review pair's completion so the review window has closed, rather than
    /// waiting fourteen days.
    /// </summary>
    public static async Task ExpireReviewWindowAsync(
        this GoSitApiFactory factory,
        Guid careBookingId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        await context.ReviewPairs
            .Where(p => p.CareBookingId == careBookingId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                p => p.CompletedAtUtc, DateTime.UtcNow.AddDays(-30)));
    }

    public static Task<ReviewSweepResult> RunReviewSweepAsync(this GoSitApiFactory factory) =>
        factory.Services.GetRequiredService<ReviewPublicationSweeper>()
            .SweepAsync(CancellationToken.None);

    /// <summary>Drives a booking all the way to Completed so it can be reviewed.</summary>
    public static async Task<Guid> CompletedBookingAsync(
        this GoSitApiFactory factory,
        HttpClient requester,
        Guid caregiverProfileId,
        Guid careRecipientId,
        DateTime startAtUtc,
        DateTime endAtUtc)
    {
        var booking = await requester.CreateBookingAsync(
            caregiverProfileId, careRecipientId, startAtUtc, endAtUtc);

        var caregiver = await factory.CaregiverClientForAsync(caregiverProfileId);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);

        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - startAtUtc);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null);

        var checkedOut = await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-out", null);
        checkedOut.EnsureSuccessStatusCode();

        return booking.Id;
    }
}
