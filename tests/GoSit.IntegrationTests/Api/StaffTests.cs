using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Creating staff. The rules worth pinning: only a super administrator can add people; the new
/// person chooses their own password through the emailed link (nobody else ever knows it); and
/// the account they get is an ordinary administrator, who cannot go on to add more.
/// </summary>
public class StaffTests(RecoveryFactoryFixture factory) : IClassFixture<RecoveryFactoryFixture>
{
    private const string NewPassword = "a-long-passphrase-for-testing";

    private static object Invite(string email) =>
        new { email, firstName = "Pat", lastName = "Reviewer" };

    [Fact]
    public async Task A_super_administrator_can_invite_staff_who_choose_their_own_password()
    {
        var superAdmin = await factory.RegisterAdminAsync(superAdmin: true);
        var email = $"staff-{Guid.NewGuid():N}@example.com";

        var invited = await superAdmin.PostAsJsonAsync("/api/v1/admin/staff", Invite(email));

        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);

        var (_, invitedBy, token) = Assert.Single(
            factory.Notifier.StaffInvitations, i => i.Email == email);
        Assert.False(string.IsNullOrWhiteSpace(invitedBy));

        // Until they choose a password the account cannot be used: nobody knows its password.
        var before = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = "anything" });
        Assert.Equal(HttpStatusCode.Unauthorized, before.StatusCode);

        var reset = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm", new { token, newPassword = NewPassword });
        Assert.True(reset.IsSuccessStatusCode);

        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = NewPassword });
        login.EnsureSuccessStatusCode();

        var tokens = await login.Content.ReadFromJsonAsync<ProfileTestClient.Tokens>();
        Assert.Equal("Admin", tokens!.Role);

        // An ordinary administrator: can work the queues, cannot add staff.
        var staffClient = factory.CreateClient();
        staffClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await staffClient.GetAsync("/api/v1/admin/verifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.GetAsync("/api/v1/admin/staff")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await staffClient.PostAsJsonAsync("/api/v1/admin/staff", Invite($"x-{Guid.NewGuid():N}@example.com"))).StatusCode);
    }

    [Fact]
    public async Task An_ordinary_administrator_cannot_list_or_invite_staff()
    {
        var admin = await factory.RegisterAdminAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/staff")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await admin.PostAsJsonAsync("/api/v1/admin/staff", Invite($"x-{Guid.NewGuid():N}@example.com"))).StatusCode);
    }

    [Fact]
    public async Task A_family_or_caregiver_cannot_reach_staff_management()
    {
        var caregiver = await factory.RegisterCaregiverAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await caregiver.GetAsync("/api/v1/admin/staff")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await caregiver.PostAsJsonAsync("/api/v1/admin/staff", Invite($"x-{Guid.NewGuid():N}@example.com"))).StatusCode);
    }

    [Fact]
    public async Task Signed_out_requests_are_refused()
    {
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/staff")).StatusCode);
    }

    [Fact]
    public async Task An_email_that_already_has_an_account_is_refused()
    {
        var superAdmin = await factory.RegisterAdminAsync(superAdmin: true);
        var existing = $"taken-{Guid.NewGuid():N}@example.com";

        (await superAdmin.PostAsJsonAsync("/api/v1/admin/staff", Invite(existing)))
            .EnsureSuccessStatusCode();

        var again = await superAdmin.PostAsJsonAsync("/api/v1/admin/staff", Invite(existing));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_bad_email_is_reported_against_the_field()
    {
        var superAdmin = await factory.RegisterAdminAsync(superAdmin: true);

        var response = await superAdmin.PostAsJsonAsync("/api/v1/admin/staff", Invite("not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("Email", out _));
    }

    [Fact]
    public async Task The_staff_list_shows_who_is_a_super_administrator()
    {
        var superAdmin = await factory.RegisterAdminAsync(superAdmin: true);
        var email = $"listed-{Guid.NewGuid():N}@example.com";
        (await superAdmin.PostAsJsonAsync("/api/v1/admin/staff", Invite(email))).EnsureSuccessStatusCode();

        var staff = await superAdmin.GetFromJsonAsync<List<StaffBody>>("/api/v1/admin/staff");

        Assert.Contains(staff!, s => s.Email == email && !s.IsSuperAdmin);
        Assert.Contains(staff!, s => s.IsSuperAdmin);
    }

    [Fact]
    public async Task Me_reports_whether_the_caller_is_a_super_administrator()
    {
        var superAdmin = await factory.RegisterAdminAsync(superAdmin: true);
        var admin = await factory.RegisterAdminAsync();

        Assert.True((await superAdmin.GetFromJsonAsync<MeBody>("/api/v1/admin/staff/me"))!.IsSuperAdmin);
        Assert.False((await admin.GetFromJsonAsync<MeBody>("/api/v1/admin/staff/me"))!.IsSuperAdmin);
    }

    private sealed record StaffBody(Guid Id, string Email, bool IsSuperAdmin);

    private sealed record MeBody(bool IsSuperAdmin);
}
