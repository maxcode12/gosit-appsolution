using System.Net;
using System.Net.Http.Json;
using GoSit.Domain.Caregivers;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Booking care that runs past midnight.
///
/// GoSit's service catalogue has offered "Overnight Child Care" since the reference data
/// was seeded, and until now every attempt to book it was refused — the weekly
/// availability model could not express a shift crossing midnight, so the check returned
/// "this caregiver is not available at that time" and blamed the caregiver for a gap in
/// our own model.
///
/// These hold the fix at the level a family experiences it: the service on the storefront
/// can actually be bought.
/// </summary>
[Collection(nameof(ApiCollection))]
public class OvernightBookingTests(GoSitApiFactory factory)
{
    /// <summary>
    /// A Friday-night shift into Saturday morning, in Toronto local time — which is what
    /// availability is stored in. Expressed as UTC because the API takes UTC.
    /// </summary>
    private static DateTime LocalToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
            TimeZoneInfo.FindSystemTimeZoneById("America/Toronto"));

    /// <summary>What the seeder requires before overnight child care can be booked.</summary>
    private static readonly string[] OvernightCertifications =
        ["VULNERABLE_SECTOR", "FIRST_AID", "CPR"];

    /// <summary>The next Friday at 20:00 local, and the Saturday 06:00 that follows.</summary>
    private static (DateTime StartUtc, DateTime EndUtc) OvernightWindow()
    {
        var today = DateTime.UtcNow.Date;
        var daysUntilFriday = ((int)DayOfWeek.Friday - (int)today.DayOfWeek + 7) % 7;

        // At least a week out, so the booking is comfortably in the future whatever day
        // the suite runs on.
        var friday = today.AddDays(daysUntilFriday == 0 ? 7 : daysUntilFriday + 7);

        return (
            LocalToUtc(friday.AddHours(20)),
            LocalToUtc(friday.AddDays(1).AddHours(6)));
    }

    /// <summary>
    /// Replaces a caregiver's schedule with one overnight window.
    ///
    /// Written straight to the database rather than through the API because
    /// CreateBookableCaregiverAsync has already set a daytime schedule, and this is about
    /// the booking rules rather than the availability endpoint — which
    /// <see cref="CaregiverProfileTests"/> covers.
    /// </summary>
    private async Task GiveOvernightAvailabilityAsync(Guid caregiverProfileId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var profile = await context.CaregiverProfiles
            .Include(p => p.Availability)
            .SingleAsync(p => p.Id == caregiverProfileId);

        profile.ReplaceAvailability(
            [new AvailabilityWindow(DayOfWeek.Friday, new TimeOnly(19, 0), new TimeOnly(8, 0))],
            DateTime.UtcNow);

        await context.SaveChangesAsync();
    }

    private async Task<(HttpClient Requester, Guid RecipientId)> RequesterAsync()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();

        return (client, await client.AddCareRecipientAsync());
    }

    [Fact]
    public async Task An_overnight_booking_is_accepted_when_the_caregiver_works_nights()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync(
            serviceCode: "OVERNIGHT_CHILD_CARE",
            certificationCodes: OvernightCertifications);

        await GiveOvernightAvailabilityAsync(caregiverId);

        var (requester, recipientId) = await RequesterAsync();
        var (startUtc, endUtc) = OvernightWindow();

        var response = await requester.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = caregiverId,
            careServiceCode = "OVERNIGHT_CHILD_CARE",
            careRecipientIds = new[] { recipientId },
            startAtUtc = startUtc,
            endAtUtc = endUtc,
            address = (object?)null,
            instructions = "Bedtime is eight. She wakes early.",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var booking = (await response.Content.ReadFromJsonAsync<BookingBody>())!;

        Assert.Equal("Pending", booking.Status);
        Assert.Equal("OVERNIGHT_CHILD_CARE", booking.CareServiceCode);
    }

    /// <summary>
    /// The fix must not turn "available" into "always". A caregiver whose night ends at
    /// 08:00 has not agreed to work until noon.
    /// </summary>
    [Fact]
    public async Task An_overnight_booking_past_the_end_of_the_window_is_still_refused()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync(
            serviceCode: "OVERNIGHT_CHILD_CARE",
            certificationCodes: OvernightCertifications);

        await GiveOvernightAvailabilityAsync(caregiverId);

        var (requester, recipientId) = await RequesterAsync();
        var (startUtc, _) = OvernightWindow();

        // 20:00 Friday to 12:00 Saturday, four hours past the caregiver's 08:00 finish.
        var endUtc = startUtc.AddHours(16);

        var response = await requester.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = caregiverId,
            careServiceCode = "OVERNIGHT_CHILD_CARE",
            careRecipientIds = new[] { recipientId },
            startAtUtc = startUtc,
            endAtUtc = endUtc,
            address = (object?)null,
            instructions = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// A caregiver who only works days must not be bookable overnight. The circular
    /// comparison could plausibly wrap a daytime window round to the small hours, and
    /// that would put a stranger in a house at 2am on nobody's agreement.
    /// </summary>
    [Fact]
    public async Task A_daytime_caregiver_is_not_bookable_overnight()
    {
        // CreateBookableCaregiverAsync sets a schedule of 00:00-23:59 every day, which
        // never crosses midnight — wide open by day, closed overnight.
        var caregiverId = await factory.CreateBookableCaregiverAsync(
            serviceCode: "OVERNIGHT_CHILD_CARE",
            certificationCodes: OvernightCertifications);

        var (requester, recipientId) = await RequesterAsync();
        var (startUtc, endUtc) = OvernightWindow();

        var response = await requester.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = caregiverId,
            careServiceCode = "OVERNIGHT_CHILD_CARE",
            careRecipientIds = new[] { recipientId },
            startAtUtc = startUtc,
            endAtUtc = endUtc,
            address = (object?)null,
            instructions = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Searching across midnight ranks the caregiver who works nights above the one who
    /// does not.
    ///
    /// Ranked rather than filtered, which is the existing design: availability decides
    /// order, not visibility, so a family can still see and message someone whose usual
    /// hours do not fit. Before this change an overnight search was refused outright as a
    /// bad request.
    /// </summary>
    [Fact]
    public async Task An_overnight_search_ranks_night_workers_first()
    {
        var city = $"Nightshift{Guid.NewGuid():N}"[..20];

        var days = await factory.CreateListableCaregiverAsync(
            city: city,
            serviceCodes: ["OVERNIGHT_CHILD_CARE"],
            certificationCodes: OvernightCertifications,
            availability: [(DayOfWeek.Friday, "09:00:00", "17:00:00")]);

        var nights = await factory.CreateListableCaregiverAsync(
            city: city,
            serviceCodes: ["OVERNIGHT_CHILD_CARE"],
            certificationCodes: OvernightCertifications,
            availability: [(DayOfWeek.Friday, "19:00:00", "08:00:00")]);

        var (requester, _) = await RequesterAsync();

        var results = await requester.GetFromJsonAsync<SearchTestFixtures.SearchBody>(
            $"/api/v1/caregivers/search?city={city}&day=Friday&startTime=21:00:00&endTime=06:00:00");

        var order = results!.Results.Select(r => r.CaregiverProfileId).ToList();

        Assert.Contains(nights, order);
        Assert.Contains(days, order);
        Assert.True(
            order.IndexOf(nights) < order.IndexOf(days),
            "A caregiver who works nights should rank above one who does not.");
    }
}
