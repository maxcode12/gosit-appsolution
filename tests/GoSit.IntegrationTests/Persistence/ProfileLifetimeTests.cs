using GoSit.Domain.CareRecipients;
using GoSit.Domain.CareRequesters;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Users;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GoSit.IntegrationTests.Persistence;

/// <summary>
/// Deleting an account must take its profile, care recipients, and — most importantly —
/// their care needs with it. Without a foreign key from the profile to the account, that
/// sensitive data is orphaned in the database and survives the deletion.
/// </summary>
[Collection(nameof(Api.ApiCollection))]
public class ProfileLifetimeTests(Api.GoSitApiFactory factory)
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Deleting_an_account_removes_its_care_recipients_and_care_needs()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var user = User.Register(
            $"lifetime-{Guid.NewGuid():N}@example.com", "hash", "Ada", "Lovelace",
            UserRole.CareRequester, Now);
        context.Users.Add(user);

        var profile = CareRequesterProfile.Create(user.Id, "416-555-0142", Now);
        var recipient = profile.AddCareRecipient(
            "Sam", null, new DateOnly(2018, 4, 2), CareRecipientType.Child, Now);
        profile.UpdateCareNeeds(recipient.Id, new CareNeedsDetails { Allergies = "Peanuts" }, Now);
        context.CareRequesterProfiles.Add(profile);

        await context.SaveChangesAsync(CancellationToken.None);

        var careNeedsBefore = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM care_needs_profiles WHERE \"CareRecipientId\" = {recipient.Id}")
            .SingleAsync(CancellationToken.None);
        Assert.Equal(1, careNeedsBefore);

        context.Users.Remove(user);
        await context.SaveChangesAsync(CancellationToken.None);

        Assert.False(await context.CareRequesterProfiles
            .AnyAsync(p => p.UserId == user.Id, CancellationToken.None));
        Assert.False(await context.CareRecipients
            .AnyAsync(r => r.Id == recipient.Id, CancellationToken.None));

        var careNeedsAfter = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM care_needs_profiles WHERE \"CareRecipientId\" = {recipient.Id}")
            .SingleAsync(CancellationToken.None);
        Assert.Equal(0, careNeedsAfter);
    }

    [Fact]
    public async Task Deleting_an_account_removes_its_caregiver_profile_and_availability()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var user = User.Register(
            $"lifetime-{Guid.NewGuid():N}@example.com", "hash", "Bo", "Nkemi",
            UserRole.Caregiver, Now);
        context.Users.Add(user);

        var profile = CaregiverProfile.Create(user.Id, 25m, 3, Now);
        profile.ReplaceAvailability(
            [new AvailabilityWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))], Now);
        context.CaregiverProfiles.Add(profile);

        await context.SaveChangesAsync(CancellationToken.None);

        context.Users.Remove(user);
        await context.SaveChangesAsync(CancellationToken.None);

        Assert.False(await context.CaregiverProfiles
            .AnyAsync(p => p.UserId == user.Id, CancellationToken.None));

        var slots = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM availability_slots WHERE \"CaregiverProfileId\" = {profile.Id}")
            .SingleAsync(CancellationToken.None);
        Assert.Equal(0, slots);
    }
}
