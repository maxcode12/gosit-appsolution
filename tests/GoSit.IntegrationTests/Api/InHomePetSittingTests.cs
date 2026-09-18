using System.Net;
using System.Net.Http.Json;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// In-home pet sitting: a sitter staying over at the family's home, priced per night.
///
/// It shares boarding's pricing and schedule rules and none of its location rules. The
/// booking carries the family's address, so the usual disclosure holds — the sitter sees it
/// only once they accept.
/// </summary>
[Collection(nameof(ApiCollection))]
public class InHomePetSittingTests(GoSitApiFactory factory)
{
    private static readonly DateTime Arrives = NextWeekdayAtUtc(DayOfWeek.Thursday, 15);

    private sealed record AddressBody(string Line1);

    private sealed record StayBody(
        Guid Id,
        AddressBody? Address,
        string? Instructions,
        string PricingUnit,
        int? Nights,
        string CareLocation,
        decimal EstimatedTotal,
        bool AddressDisclosed);

    private sealed record PetBody(Guid Id);

    /// <summary>A sitter whose weekly hours are Monday only, so a Thursday stay proves hours are not asked.</summary>
    private async Task<Guid> SitterAsync(decimal? nightlyRate = 60m)
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["IN_HOME_PET_SITTING"],
            certificationCodes: [],
            availability: [(DayOfWeek.Monday, "09:00:00", "17:00:00")]);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        await context.CaregiverProfiles
            .Where(p => p.Id == caregiverId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.NightlyRate, nightlyRate));

        return caregiverId;
    }

    private async Task<(HttpClient Owner, StayBody Stay, HttpResponseMessage Response)> BookAsync(
        Guid sitterId, DateTime leaves)
    {
        var owner = await factory.RegisterCareRequesterAsync();
        await owner.CreateCareRequesterProfileAsync();

        var pet = await (await owner.PostAsJsonAsync(
                "/api/v1/care-requesters/me/pets", new { name = "Mochi", species = "Cat" }))
            .Content.ReadFromJsonAsync<PetBody>();

        var response = await owner.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = sitterId,
            careServiceCode = "IN_HOME_PET_SITTING",
            petIds = new[] { pet!.Id },
            startAtUtc = Arrives,
            endAtUtc = leaves,
            instructions = "Door code 4412. Back Sunday evening.",
        });

        var stay = response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<StayBody>())!
            : null!;

        return (owner, stay, response);
    }

    [Fact]
    public async Task In_home_sitting_is_priced_per_night_at_the_familys_home()
    {
        var sitterId = await SitterAsync(nightlyRate: 60m);

        var (_, stay, response) = await BookAsync(sitterId, Arrives.AddDays(2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Nightly", stay.PricingUnit);
        Assert.Equal("RecipientHome", stay.CareLocation);
        Assert.Equal(2, stay.Nights);
        Assert.Equal(120m, stay.EstimatedTotal);
    }

    /// <summary>A sitting that ends the same day is not a night's care; drop-in visits cover it.</summary>
    [Fact]
    public async Task In_home_sitting_must_include_a_night()
    {
        var sitterId = await SitterAsync();

        var (_, _, response) = await BookAsync(sitterId, Arrives.AddHours(4));

        Assert.InRange((int)response.StatusCode, 400, 499);
    }

    [Fact]
    public async Task A_sitter_without_a_nightly_rate_cannot_be_booked()
    {
        var sitterId = await SitterAsync(nightlyRate: null);

        var (_, _, response) = await BookAsync(sitterId, Arrives.AddDays(1));

        Assert.InRange((int)response.StatusCode, 400, 499);
    }

    /// <summary>
    /// Unlike boarding, the address is the family's — so the sitter sees it, and the door
    /// code, only once they accept. The family always sees their own.
    /// </summary>
    [Fact]
    public async Task The_sitter_sees_the_familys_address_only_once_they_accept()
    {
        var sitterId = await SitterAsync();
        var (owner, stay, _) = await BookAsync(sitterId, Arrives.AddDays(2));

        var sitter = await factory.CaregiverClientForAsync(sitterId);

        var request = await sitter.GetFromJsonAsync<StayBody>($"/api/v1/bookings/{stay.Id}");
        Assert.False(request!.AddressDisclosed);
        Assert.Null(request.Address);
        Assert.Null(request.Instructions);

        Assert.NotNull((await owner.GetFromJsonAsync<StayBody>($"/api/v1/bookings/{stay.Id}"))!.Address);

        (await sitter.PostAsync($"/api/v1/bookings/{stay.Id}/accept", null))
            .EnsureSuccessStatusCode();

        var accepted = await sitter.GetFromJsonAsync<StayBody>($"/api/v1/bookings/{stay.Id}");
        Assert.NotNull(accepted!.Address);
        Assert.Contains("Door code", accepted.Instructions);
    }
}
