using System.Net;
using System.Net.Http.Json;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Pet care: pets on a family's profile, booking a sitter for them, the screening line
/// between pet care and care for people, and the household details a request withholds.
/// </summary>
[Collection(nameof(ApiCollection))]
public class PetCareTests(GoSitApiFactory factory)
{
    private static readonly DateTime Start = NextWeekdayAtUtc(DayOfWeek.Thursday, 15);

    private const string DoorCode = "Door code 4412. We are away until Sunday.";

    private sealed record PetBody(
        Guid Id,
        string Name,
        string Species,
        string? Breed,
        string? Size,
        string? FeedingInstructions,
        string? Behaviour);

    private sealed record ProfileBody(IReadOnlyList<PetBody> Pets);

    private sealed record AreaBody(string City, string Province, string PostalCodePrefix);

    private sealed record AddressBody(string Line1, string City, string PostalCode);

    private sealed record PetNotesBody(string? FeedingInstructions, string? Behaviour);

    private sealed record BookingPetBody(Guid PetId, string Name, string Species, PetNotesBody? CareNotes);

    private sealed record PetBookingBody(
        Guid Id,
        string Status,
        AddressBody? Address,
        AreaBody Area,
        string? Instructions,
        IReadOnlyList<BookingPetBody> Pets,
        bool HouseholdDetailsDisclosed);

    private sealed record VerificationCaseBody(Guid Id);

    private sealed record VerificationSummaryBody(
        string VerificationStatus,
        bool MeetsListingRequirements,
        IReadOnlyList<string> RequiredChecks,
        bool ClearedForPeopleCare);

    // ---- helpers ----

    private async Task<(HttpClient Requester, Guid PetId)> FamilyWithPetAsync(string species = "Dog")
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();

        var response = await requester.PostAsJsonAsync("/api/v1/care-requesters/me/pets", new
        {
            name = "Biscuit",
            species,
            breed = "Labrador",
            size = "Large",
            feedingInstructions = "One cup at 6pm.",
            behaviour = "Reactive to other dogs on the lead.",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var pet = await response.Content.ReadFromJsonAsync<PetBody>();
        return (requester, pet!.Id);
    }

    private static Task<HttpResponseMessage> BookPetCareAsync(
        HttpClient requester,
        Guid caregiverId,
        Guid petId,
        string serviceCode = "DOG_WALKING",
        int minutes = 30) =>
        requester.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = caregiverId,
            careServiceCode = serviceCode,
            petIds = new[] { petId },
            startAtUtc = Start,
            endAtUtc = Start.AddMinutes(minutes),
            instructions = DoorCode,
        });

    private Task<Guid> DogWalkerAsync() =>
        factory.CreateBookableCaregiverAsync(serviceCode: "DOG_WALKING", certificationCodes: []);

    /// <summary>Withdraws a listed caregiver's clearance for people, leaving them cleared for pets.</summary>
    private async Task ClearForPetsOnlyAsync(Guid caregiverProfileId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        await context.CaregiverProfiles
            .Where(p => p.Id == caregiverProfileId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ClearedForPeopleCare, false));
    }

    private static async Task<PetBookingBody> ReadAsync(HttpClient client, Guid bookingId) =>
        (await client.GetFromJsonAsync<PetBookingBody>($"/api/v1/bookings/{bookingId}"))!;

    // ---- pets on the profile ----

    [Fact]
    public async Task A_pet_is_kept_on_the_family_profile()
    {
        var (requester, petId) = await FamilyWithPetAsync();

        var profile = await requester.GetFromJsonAsync<ProfileBody>("/api/v1/care-requesters/me");

        var pet = Assert.Single(profile!.Pets, p => p.Id == petId);
        Assert.Equal("Dog", pet.Species);
        Assert.Equal("One cup at 6pm.", pet.FeedingInstructions);
    }

    [Fact]
    public async Task A_pet_can_be_edited_and_removed()
    {
        var (requester, petId) = await FamilyWithPetAsync();

        var updated = await requester.PutAsJsonAsync($"/api/v1/care-requesters/me/pets/{petId}", new
        {
            name = "Biscuit",
            species = "Dog",
            size = "Medium",
            behaviour = "Much calmer since training.",
        });

        updated.EnsureSuccessStatusCode();
        Assert.Equal("Medium", (await updated.Content.ReadFromJsonAsync<PetBody>())!.Size);

        var removed = await requester.DeleteAsync($"/api/v1/care-requesters/me/pets/{petId}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var profile = await requester.GetFromJsonAsync<ProfileBody>("/api/v1/care-requesters/me");
        Assert.Empty(profile!.Pets);
    }

    [Fact]
    public async Task Another_familys_pet_is_not_found()
    {
        var (_, petId) = await FamilyWithPetAsync();

        var stranger = await factory.RegisterCareRequesterAsync();
        await stranger.CreateCareRequesterProfileAsync();

        var response = await stranger.DeleteAsync($"/api/v1/care-requesters/me/pets/{petId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_pet_needs_a_name()
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();

        var response = await requester.PostAsJsonAsync(
            "/api/v1/care-requesters/me/pets", new { name = "", species = "Cat" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- booking pet care ----

    [Fact]
    public async Task A_dog_walk_can_be_booked_for_half_an_hour()
    {
        var walkerId = await DogWalkerAsync();
        var (requester, petId) = await FamilyWithPetAsync();

        var response = await BookPetCareAsync(requester, walkerId, petId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var booking = await response.Content.ReadFromJsonAsync<PetBookingBody>();
        var pet = Assert.Single(booking!.Pets);
        Assert.Equal("Biscuit", pet.Name);
        Assert.Equal("Dog", pet.Species);
    }

    [Fact]
    public async Task Dog_walking_is_not_offered_for_a_cat()
    {
        var walkerId = await DogWalkerAsync();
        var (requester, catId) = await FamilyWithPetAsync(species: "Cat");

        var response = await BookPetCareAsync(requester, walkerId, catId);

        Assert.InRange((int)response.StatusCode, 400, 499);
    }

    [Fact]
    public async Task Pet_care_cannot_name_a_person()
    {
        var walkerId = await DogWalkerAsync();

        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        var response = await requester.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = walkerId,
            careServiceCode = "DOG_WALKING",
            careRecipientIds = new[] { recipientId },
            startAtUtc = Start,
            endAtUtc = Start.AddHours(1),
        });

        Assert.InRange((int)response.StatusCode, 400, 499);
    }

    [Fact]
    public async Task Care_for_people_cannot_name_a_pet()
    {
        var babysitterId = await factory.CreateBookableCaregiverAsync();
        var (requester, petId) = await FamilyWithPetAsync();

        var response = await BookPetCareAsync(requester, babysitterId, petId, serviceCode: "BABYSITTING", minutes: 120);

        Assert.InRange((int)response.StatusCode, 400, 499);
    }

    /// <summary>
    /// The screening line, end to end. A caregiver whose checks clear them only for pets can
    /// list Babysitting and still never be booked for a child.
    /// </summary>
    [Fact]
    public async Task A_caregiver_cleared_only_for_pets_cannot_be_booked_for_a_child()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        (await caregiver.PutAsJsonAsync("/api/v1/caregivers/me/services",
            new { serviceCodes = new[] { "BABYSITTING", "DOG_WALKING" } }))
            .EnsureSuccessStatusCode();

        await ClearForPetsOnlyAsync(caregiverId);

        var parent = await factory.RegisterCareRequesterAsync();
        await parent.CreateCareRequesterProfileAsync();
        var childId = await parent.AddCareRecipientAsync();

        var forChild = await parent.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = caregiverId,
            careServiceCode = "BABYSITTING",
            careRecipientIds = new[] { childId },
            startAtUtc = Start,
            endAtUtc = Start.AddHours(3),
        });

        Assert.InRange((int)forChild.StatusCode, 400, 499);

        var (petOwner, petId) = await FamilyWithPetAsync();
        var forDog = await BookPetCareAsync(petOwner, caregiverId, petId);

        Assert.Equal(HttpStatusCode.Created, forDog.StatusCode);
    }

    /// <summary>
    /// Clearance through the real review path: photo ID and a criminal record check, approved
    /// by staff, verify a caregiver for pet care and not for people.
    /// </summary>
    [Fact]
    public async Task Photo_ID_and_a_record_check_verify_a_pet_sitter_for_pets_only()
    {
        var caregiver = await factory.RegisterCaregiverAsync();

        (await caregiver.PutAsJsonAsync("/api/v1/caregivers/me", new
        {
            bio = "Dog walker in the Annex.",
            yearsOfExperience = 2,
            hourlyRate = 22m,
            address = new
            {
                line1 = "12 Bloor St W",
                line2 = (string?)null,
                city = "Toronto",
                province = "ON",
                postalCode = "M5S 1M2",
                country = "CA",
            },
            serviceRadiusKm = 10,
        })).EnsureSuccessStatusCode();

        (await caregiver.PutAsJsonAsync("/api/v1/caregivers/me/services",
            new { serviceCodes = new[] { "DOG_WALKING" } }))
            .EnsureSuccessStatusCode();

        var admin = await factory.RegisterAdminAsync();

        foreach (var checkType in new[] { "Identity", "BackgroundCheck" })
        {
            var submitted = await caregiver.PostAsJsonAsync(
                "/api/v1/caregivers/me/verifications",
                new { checkType, documentReference = (string?)null });

            submitted.EnsureSuccessStatusCode();
            var verificationCase = await submitted.Content.ReadFromJsonAsync<VerificationCaseBody>();

            (await admin.PostAsJsonAsync(
                $"/api/v1/admin/verifications/{verificationCase!.Id}/approve", new { }))
                .EnsureSuccessStatusCode();
        }

        var summary = await caregiver.GetFromJsonAsync<VerificationSummaryBody>(
            "/api/v1/caregivers/me/verifications");

        Assert.Equal("Verified", summary!.VerificationStatus);
        Assert.True(summary.MeetsListingRequirements);
        Assert.False(summary.ClearedForPeopleCare);
        Assert.Equal(["Identity", "BackgroundCheck"], summary.RequiredChecks);
    }

    // ---- what a request discloses ----

    /// <summary>
    /// The disclosure fix. A request goes to someone who has agreed to nothing, so it carries
    /// the area and the pet's name and size — not the address, the door code, or the notes.
    /// </summary>
    [Fact]
    public async Task A_caregiver_sees_the_area_but_not_the_household_until_they_accept()
    {
        var walkerId = await DogWalkerAsync();
        var (requester, petId) = await FamilyWithPetAsync();

        var created = await (await BookPetCareAsync(requester, walkerId, petId))
            .Content.ReadFromJsonAsync<PetBookingBody>();

        var walker = await factory.CaregiverClientForAsync(walkerId);

        var request = await ReadAsync(walker, created!.Id);

        Assert.False(request.HouseholdDetailsDisclosed);
        Assert.Null(request.Address);
        Assert.Null(request.Instructions);
        Assert.Equal(3, request.Area.PostalCodePrefix.Length);

        // Enough to decide whether to take it: it is a large dog called Biscuit.
        var pet = Assert.Single(request.Pets);
        Assert.Equal("Biscuit", pet.Name);
        Assert.Null(pet.CareNotes);

        (await walker.PostAsync($"/api/v1/bookings/{created.Id}/accept", null))
            .EnsureSuccessStatusCode();

        var accepted = await ReadAsync(walker, created.Id);

        Assert.True(accepted.HouseholdDetailsDisclosed);
        Assert.NotNull(accepted.Address);
        Assert.Equal(DoorCode, accepted.Instructions);
        Assert.Equal("Reactive to other dogs on the lead.", Assert.Single(accepted.Pets).CareNotes?.Behaviour);
    }

    [Fact]
    public async Task The_caregiver_list_withholds_the_address_of_a_request_too()
    {
        var walkerId = await DogWalkerAsync();
        var (requester, petId) = await FamilyWithPetAsync();

        var created = await (await BookPetCareAsync(requester, walkerId, petId))
            .Content.ReadFromJsonAsync<PetBookingBody>();

        var walker = await factory.CaregiverClientForAsync(walkerId);

        var list = await walker.GetFromJsonAsync<List<PetBookingBody>>("/api/v1/bookings");

        var row = Assert.Single(list!, b => b.Id == created!.Id);
        Assert.Null(row.Address);
        Assert.Null(row.Instructions);

        // The list loads the pet too — a sitter scanning requests needs to know it is a dog.
        Assert.Equal("Biscuit", Assert.Single(row.Pets).Name);
    }

    /// <summary>Declining is not a way to keep the details: they are never disclosed at all.</summary>
    [Fact]
    public async Task A_declined_request_never_discloses_the_household()
    {
        var walkerId = await DogWalkerAsync();
        var (requester, petId) = await FamilyWithPetAsync();

        var created = await (await BookPetCareAsync(requester, walkerId, petId))
            .Content.ReadFromJsonAsync<PetBookingBody>();

        var walker = await factory.CaregiverClientForAsync(walkerId);

        var declined = await walker.PostAsJsonAsync(
            $"/api/v1/bookings/{created!.Id}/decline", new { reason = "Too far." });

        declined.EnsureSuccessStatusCode();

        var body = await declined.Content.ReadFromJsonAsync<PetBookingBody>();
        Assert.Null(body!.Address);
        Assert.Null((await ReadAsync(walker, created.Id)).Instructions);
    }

    [Fact]
    public async Task The_family_always_sees_their_own_household_details()
    {
        var walkerId = await DogWalkerAsync();
        var (requester, petId) = await FamilyWithPetAsync();

        var created = await (await BookPetCareAsync(requester, walkerId, petId))
            .Content.ReadFromJsonAsync<PetBookingBody>();

        var own = await ReadAsync(requester, created!.Id);

        Assert.True(own.HouseholdDetailsDisclosed);
        Assert.NotNull(own.Address);
        Assert.Equal(DoorCode, own.Instructions);
    }

    /// <summary>The same rule for care for people — this was never specific to pets.</summary>
    [Fact]
    public async Task A_request_for_a_child_withholds_the_address_from_the_caregiver()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();

        var parent = await factory.RegisterCareRequesterAsync();
        await parent.CreateCareRequesterProfileAsync();
        var childId = await parent.AddCareRecipientAsync();

        var booking = await parent.CreateBookingAsync(caregiverId, childId, Start, Start.AddHours(3));

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        Assert.Null((await ReadAsync(caregiver, booking.Id)).Address);
    }
}
