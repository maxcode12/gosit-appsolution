using System.Net.Http.Json;
using GoSit.Domain.Bookings;
using GoSit.Infrastructure.Persistence;
using GoSit.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GoSit.IntegrationTests.Api;

public static class BookingTestFixtures
{
    public sealed record BookingBody(
        Guid Id,
        string Status,
        Guid CaregiverProfileId,
        string CaregiverFirstName,
        string CaregiverLastInitial,
        string CareRequesterFirstName,
        string CareRequesterLastInitial,
        Guid CareServiceId,
        string CareServiceCode,
        string CareServiceName,
        DateTime StartAtUtc,
        DateTime EndAtUtc,
        AddressBody Address,
        string? Instructions,
        decimal HourlyRate,
        decimal EstimatedTotal,
        decimal? ActualTotal,
        DateTime? CheckedInAtUtc,
        DateTime? CheckedOutAtUtc,
        string CheckInVerification,
        string CheckOutVerification,
        bool WasAutoCompleted,
        string? CancelledBy,
        bool IsLateCancellation,
        string? ResolutionReason,
        IReadOnlyList<RecipientBody> Recipients,
        DateTime CreatedAtUtc);

    public sealed record AddressBody(
        string Line1, string? Line2, string City, string Province, string PostalCode, string Country);

    public sealed record RecipientBody(
        Guid CareRecipientId, string FirstName, int Age, string RecipientType, bool HasRecordedCareNeeds);

    /// <summary>
    /// The next occurrence of a weekday at a fixed UTC hour, far enough ahead to clear
    /// the minimum-notice rule. Tests must not depend on when they happen to run.
    /// </summary>
    public static DateTime NextWeekdayAtUtc(DayOfWeek day, int utcHour)
    {
        var candidate = DateTime.UtcNow.Date.AddDays(2).AddHours(utcHour);

        while (candidate.DayOfWeek != day)
        {
            candidate = candidate.AddDays(1);
        }

        return DateTime.SpecifyKind(candidate, DateTimeKind.Utc);
    }

    /// <summary>
    /// A caregiver who can actually be booked: verified, listed, qualified for the
    /// service, and available across the whole window in local time.
    /// </summary>
    public static Task<Guid> CreateBookableCaregiverAsync(
        this GoSitApiFactory factory,
        string serviceCode = "BABYSITTING",
        string[]? certificationCodes = null,
        decimal hourlyRate = 30m) =>
        factory.CreateListableCaregiverAsync(
            serviceCodes: [serviceCode],
            certificationCodes: certificationCodes ?? ["VULNERABLE_SECTOR"],
            hourlyRate: hourlyRate,
            // Wide open every day, so a test only has to pick a valid window.
            availability: Enum.GetValues<DayOfWeek>()
                .Select(day => (day, "00:00:00", "23:59:00"))
                .ToArray());

    public static async Task<BookingBody> CreateBookingAsync(
        this HttpClient requester,
        Guid caregiverProfileId,
        Guid careRecipientId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string serviceCode = "BABYSITTING")
    {
        var response = await requester.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId,
            careServiceCode = serviceCode,
            careRecipientIds = new[] { careRecipientId },
            startAtUtc,
            endAtUtc,
            address = (object?)null,
            instructions = "Bedtime is 8pm.",
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BookingBody>())!;
    }

    /// <summary>The HTTP client for the account that owns a caregiver profile.</summary>
    public static async Task<HttpClient> CaregiverClientForAsync(
        this GoSitApiFactory factory,
        Guid caregiverProfileId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var userId = await context.CaregiverProfiles
            .Where(p => p.Id == caregiverProfileId)
            .Select(p => p.UserId)
            .SingleAsync();

        var email = await context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .SingleAsync();

        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "correct-horse-battery-staple",
        });

        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<ProfileTestClient.Tokens>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        return client;
    }

    /// <summary>
    /// Moves a booking's schedule into the past so check-in and check-out can be
    /// exercised without waiting. Rules that depend on the clock are covered by the
    /// domain unit tests; this only makes the HTTP flow reachable.
    /// </summary>
    public static async Task ShiftScheduleAsync(
        this GoSitApiFactory factory,
        Guid bookingId,
        TimeSpan by)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        await context.CareBookings
            .Where(b => b.Id == bookingId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(b => b.StartAtUtc, b => b.StartAtUtc + by)
                .SetProperty(b => b.EndAtUtc, b => b.EndAtUtc + by));
    }

    /// <summary>
    /// Backdates a booking's check-in, so a booking the caregiver has already checked
    /// into looks like one they walked away from hours ago.
    /// </summary>
    public static async Task ShiftCheckInAsync(
        this GoSitApiFactory factory,
        Guid bookingId,
        TimeSpan by)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        await context.CareBookings
            .Where(b => b.Id == bookingId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(b => b.CheckedInAtUtc, b => b.CheckedInAtUtc + by));
    }

    /// <summary>
    /// Runs one booking sweep now, rather than waiting for the hosted service's poll
    /// interval. The sweeper is registered as itself as well as a hosted service so a
    /// test can drive a single deterministic cycle.
    /// </summary>
    public static Task<BookingSweepResult> RunBookingSweepAsync(this GoSitApiFactory factory) =>
        factory.Services.GetRequiredService<BookingSweeper>().SweepAsync(CancellationToken.None);

    public static async Task<BookingStatus> ReadStatusAsync(
        this GoSitApiFactory factory,
        Guid bookingId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.CareBookings
            .Where(b => b.Id == bookingId)
            .Select(b => b.Status)
            .SingleAsync();
    }

    public static async Task<IReadOnlyList<string>> ReadOutboxTypesAsync(
        this GoSitApiFactory factory,
        Guid bookingId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        // Payloads carry the booking id, so matching on it finds this booking's events.
        return await context.OutboxMessages
            .Where(m => m.Payload.Contains(bookingId.ToString()))
            .OrderBy(m => m.OccurredAtUtc)
            .Select(m => m.Type)
            .ToListAsync();
    }

    public static async Task<IReadOnlyList<string>> ReadOutboxPayloadsAsync(
        this GoSitApiFactory factory,
        Guid bookingId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.OutboxMessages
            .Where(m => m.Payload.Contains(bookingId.ToString()))
            .Select(m => m.Payload)
            .ToListAsync();
    }

    public static async Task<IReadOnlyList<string>> ReadAuditActionsAsync(
        this GoSitApiFactory factory,
        Guid bookingId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.AuditEvents
            .Where(a => a.EntityId == bookingId)
            .OrderBy(a => a.OccurredAtUtc)
            .Select(a => a.Action)
            .ToListAsync();
    }
}
