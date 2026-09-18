using System.Net;
using System.Net.Http.Json;
using GoSit.Domain.Bookings;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Pet boarding: a stay at the caregiver's home, priced per night.
///
/// Three things set it apart and each is pinned here: the price is per night, the stay is
/// not held to a weekly schedule of working hours, and the booking carries the caregiver's
/// address — which the family sees only once the caregiver accepts.
/// </summary>
[Collection(nameof(ApiCollection))]
public class BoardingTests(GoSitApiFactory factory)
{
    private static readonly DateTime DropOff = NextWeekdayAtUtc(DayOfWeek.Thursday, 15);

    private const string Notes = "Feed at 6pm. Lead is in the bag.";

    private sealed record AddressBody(string Line1, string City);

    private sealed record AreaBody(string City, string Province, string PostalCodePrefix);

    private sealed record StayBody(
        Guid Id,
        string Status,
        AddressBody? Address,
        AreaBody Area,
        string? Instructions,
        string PricingUnit,
        decimal? NightlyRate,
        int? Nights,
        string CareLocation,
        decimal EstimatedTotal,
        decimal? ActualTotal,
        string CheckInVerification,
        bool AddressDisclosed,
        bool HouseholdDetailsDisclosed);

    private sealed record PetBody(Guid Id);

    private sealed record OwnProfileBody(decimal? NightlyRate);

    // ---- helpers ----

    /// <summary>
    /// A caregiver offering boarding. Their weekly hours are Monday only, on purpose: a stay
    /// on any other day proves boarding is not tested against working hours.
    /// </summary>
    private async Task<Guid> BoarderAsync(decimal? nightlyRate = 45m)
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["PET_BOARDING"],
            certificationCodes: [],
            availability: [(DayOfWeek.Monday, "09:00:00", "17:00:00")]);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        await context.CaregiverProfiles
            .Where(p => p.Id == caregiverId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.NightlyRate, nightlyRate));

        return caregiverId;
    }

    private async Task<(HttpClient Owner, Guid PetId)> OwnerWithDogAsync()
    {
        var owner = await factory.RegisterCareRequesterAsync();
        await owner.CreateCareRequesterProfileAsync();

        var created = await owner.PostAsJsonAsync(
            "/api/v1/care-requesters/me/pets", new { name = "Biscuit", species = "Dog" });

        created.EnsureSuccessStatusCode();

        return (owner, (await created.Content.ReadFromJsonAsync<PetBody>())!.Id);
    }

    private static Task<HttpResponseMessage> BookStayAsync(
        HttpClient owner, Guid caregiverId, Guid petId, DateTime pickUp) =>
        owner.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = caregiverId,
            careServiceCode = "PET_BOARDING",
            petIds = new[] { petId },
            startAtUtc = DropOff,
            endAtUtc = pickUp,
            instructions = Notes,
        });

    private static async Task<StayBody> ReadAsync(HttpClient client, Guid bookingId) =>
        (await client.GetFromJsonAsync<StayBody>($"/api/v1/bookings/{bookingId}"))!;

    private async Task<(HttpClient Owner, HttpClient Boarder, StayBody Stay)> AcceptedStayAsync()
    {
        var boarderId = await BoarderAsync();
        var (owner, petId) = await OwnerWithDogAsync();

        var stay = await (await BookStayAsync(owner, boarderId, petId, DropOff.AddDays(2)))
            .Content.ReadFromJsonAsync<StayBody>();

        var boarder = await factory.CaregiverClientForAsync(boarderId);

        (await boarder.PostAsync($"/api/v1/bookings/{stay!.Id}/accept", null))
            .EnsureSuccessStatusCode();

        return (owner, boarder, stay);
    }

    // ---- pricing and schedule ----

    [Fact]
    public async Task A_stay_is_priced_per_night()
    {
        var boarderId = await BoarderAsync(nightlyRate: 45m);
        var (owner, petId) = await OwnerWithDogAsync();

        var response = await BookStayAsync(owner, boarderId, petId, DropOff.AddDays(2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var stay = await response.Content.ReadFromJsonAsync<StayBody>();

        Assert.Equal("Nightly", stay!.PricingUnit);
        Assert.Equal("CaregiverHome", stay.CareLocation);
        Assert.Equal(2, stay.Nights);
        Assert.Equal(45m, stay.NightlyRate);
        Assert.Equal(90m, stay.EstimatedTotal);
    }

    /// <summary>
    /// The boarder's hours are Monday only and this stay is Thursday to Saturday. A weekly
    /// schedule of working hours cannot say "hosting a dog all weekend", so it is not asked.
    /// </summary>
    [Fact]
    public async Task A_stay_is_not_held_to_weekly_working_hours()
    {
        var boarderId = await BoarderAsync();
        var (owner, petId) = await OwnerWithDogAsync();

        var response = await BookStayAsync(owner, boarderId, petId, DropOff.AddDays(2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>Drop-off and pick-up on the same day is daycare, not boarding.</summary>
    [Fact]
    public async Task A_stay_must_include_a_night()
    {
        var boarderId = await BoarderAsync();
        var (owner, petId) = await OwnerWithDogAsync();

        var response = await BookStayAsync(owner, boarderId, petId, DropOff.AddHours(6));

        Assert.InRange((int)response.StatusCode, 400, 499);
    }

    [Fact]
    public async Task A_caregiver_without_a_nightly_rate_cannot_be_booked_for_boarding()
    {
        var boarderId = await BoarderAsync(nightlyRate: null);
        var (owner, petId) = await OwnerWithDogAsync();

        var response = await BookStayAsync(owner, boarderId, petId, DropOff.AddDays(2));

        Assert.InRange((int)response.StatusCode, 400, 499);
    }

    [Fact]
    public async Task A_caregiver_sets_their_nightly_rate_on_their_profile()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        (await caregiver.PutAsJsonAsync("/api/v1/caregivers/me", new
        {
            bio = "I board dogs at home.",
            yearsOfExperience = 4,
            hourlyRate = 25m,
            nightlyRate = 55m,
            address = new
            {
                line1 = "12 Bloor St W",
                line2 = (string?)null,
                city = "Toronto",
                province = "ON",
                postalCode = "M5S 1M2",
                country = "CA",
            },
            serviceRadiusKm = 25,
        })).EnsureSuccessStatusCode();

        var profile = await caregiver.GetFromJsonAsync<OwnProfileBody>("/api/v1/caregivers/me");

        Assert.Equal(55m, profile!.NightlyRate);
    }

    // ---- whose address ----

    /// <summary>
    /// The address rule runs the other way for boarding. The address is the caregiver's:
    /// theirs to see, and the family's once the caregiver accepts. The family's own notes
    /// still reach the caregiver only on acceptance.
    /// </summary>
    [Fact]
    public async Task The_family_sees_the_caregivers_address_only_once_they_accept()
    {
        var boarderId = await BoarderAsync();
        var (owner, petId) = await OwnerWithDogAsync();

        var created = await (await BookStayAsync(owner, boarderId, petId, DropOff.AddDays(2)))
            .Content.ReadFromJsonAsync<StayBody>();

        var boarder = await factory.CaregiverClientForAsync(boarderId);

        var ownerBefore = await ReadAsync(owner, created!.Id);
        Assert.False(ownerBefore.AddressDisclosed);
        Assert.Null(ownerBefore.Address);
        Assert.Equal("Toronto", ownerBefore.Area.City);

        var boarderBefore = await ReadAsync(boarder, created.Id);
        Assert.Equal("12 Bloor St W", boarderBefore.Address?.Line1);
        Assert.Null(boarderBefore.Instructions);

        (await boarder.PostAsync($"/api/v1/bookings/{created.Id}/accept", null))
            .EnsureSuccessStatusCode();

        Assert.Equal("12 Bloor St W", (await ReadAsync(owner, created.Id)).Address?.Line1);
        Assert.Equal(Notes, (await ReadAsync(boarder, created.Id)).Instructions);
    }

    // ---- the stay itself ----

    /// <summary>
    /// Billed for the nights the pet actually stayed — at least one, since it was boarded.
    /// And the check-in location is not compared: the caregiver is at their own home, and
    /// "location confirmed" would read to a family as evidence of something.
    /// </summary>
    [Fact]
    public async Task A_stay_is_billed_for_the_nights_it_lasted_and_arrival_is_not_compared()
    {
        var (_, boarder, stay) = await AcceptedStayAsync();

        await factory.ShiftScheduleAsync(stay.Id, DateTime.UtcNow - DropOff);

        (await boarder.PostAsJsonAsync(
            $"/api/v1/bookings/{stay.Id}/check-in", new { latitude = 43.667, longitude = -79.395 }))
            .EnsureSuccessStatusCode();

        var checkedOut = await boarder.PostAsync($"/api/v1/bookings/{stay.Id}/check-out", null);
        checkedOut.EnsureSuccessStatusCode();

        var completed = await ReadAsync(boarder, stay.Id);

        Assert.Equal(45m, completed.ActualTotal);
        Assert.Equal("NotComparable", completed.CheckInVerification);
    }

    /// <summary>
    /// The overrun sweep closes hourly care fourteen hours after check-in. Applied to a
    /// stay, it would close every one on its first evening and bill it to the schedule.
    /// </summary>
    [Fact]
    public async Task A_stay_is_not_auto_closed_on_its_first_evening()
    {
        var (_, boarder, stay) = await AcceptedStayAsync();

        await factory.ShiftScheduleAsync(stay.Id, DateTime.UtcNow - DropOff);
        (await boarder.PostAsync($"/api/v1/bookings/{stay.Id}/check-in", null))
            .EnsureSuccessStatusCode();

        await SetTimesAsync(stay.Id, checkedIn: DateTime.UtcNow.AddHours(-20), end: DateTime.UtcNow.AddDays(1));

        await factory.RunBookingSweepAsync();

        Assert.Equal(BookingStatus.InProgress, await factory.ReadStatusAsync(stay.Id));
    }

    /// <summary>A stay nobody checked out of is closed a day after pick-up, billed to the nights scheduled.</summary>
    [Fact]
    public async Task A_stay_nobody_closed_is_completed_a_day_after_pick_up()
    {
        var (_, boarder, stay) = await AcceptedStayAsync();

        await factory.ShiftScheduleAsync(stay.Id, DateTime.UtcNow - DropOff);
        (await boarder.PostAsync($"/api/v1/bookings/{stay.Id}/check-in", null))
            .EnsureSuccessStatusCode();

        var droppedOff = DateTime.UtcNow.AddDays(-4);
        await SetTimesAsync(stay.Id, checkedIn: droppedOff, end: droppedOff.AddDays(2), start: droppedOff);

        await factory.RunBookingSweepAsync();

        var closed = await ReadAsync(boarder, stay.Id);

        Assert.Equal("Completed", closed.Status);
        Assert.Equal(90m, closed.ActualTotal);
    }

    private async Task SetTimesAsync(Guid bookingId, DateTime checkedIn, DateTime end, DateTime? start = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var booking = await context.CareBookings.SingleAsync(b => b.Id == bookingId);

        await context.CareBookings
            .Where(b => b.Id == bookingId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.CheckedInAtUtc, checkedIn)
                .SetProperty(b => b.StartAtUtc, start ?? booking.StartAtUtc)
                .SetProperty(b => b.EndAtUtc, end));
    }
}
