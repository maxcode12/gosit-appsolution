using System.Net;
using System.Net.Http.Json;
using GoSit.Domain.Users;
using GoSit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Provisioning the first staff account.
///
/// Admin accounts cannot be self-registered — correctly — but for a long time there was
/// no way to create one either, which left every admin route unreachable by anyone. The
/// existing fixtures hid that by promoting a user with SQL. These tests use the real
/// configuration-driven path, so the gap cannot reopen.
/// </summary>
public sealed class BootstrapAdminFactory : GoSitApiFactory
{
    public const string AdminEmail = "bootstrap-admin@gosit.test";
    public const string AdminPassword = "correct-horse-battery-staple";

    protected override IDictionary<string, string?> SettingOverrides =>
        new Dictionary<string, string?>
        {
            ["Administration:Bootstrap:Email"] = AdminEmail,
            ["Administration:Bootstrap:Password"] = AdminPassword,
            ["Administration:Bootstrap:FirstName"] = "Sam",
            ["Administration:Bootstrap:LastName"] = "Staff",
        };
}

public class AdministratorProvisioningTests
    : IClassFixture<BootstrapAdminFactory>, IAsyncLifetime
{
    private readonly BootstrapAdminFactory _factory;

    public AdministratorProvisioningTests(BootstrapAdminFactory factory) => _factory = factory;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// The point of the whole mechanism: staff routes are reachable without anyone
    /// editing the database.
    /// </summary>
    [Fact]
    public async Task The_configured_administrator_can_sign_in_and_reach_staff_routes()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = BootstrapAdminFactory.AdminEmail,
            password = BootstrapAdminFactory.AdminPassword,
        });

        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<ProfileTestClient.Tokens>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var queue = await client.GetAsync("/api/v1/admin/verifications");

        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
    }

    [Fact]
    public async Task The_provisioned_account_holds_the_admin_role()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var admin = await context.Users
            .AsNoTracking()
            .SingleAsync(u => u.Email == BootstrapAdminFactory.AdminEmail);

        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal(UserStatus.Active, admin.Status);

        // Created by someone who already controls the mailbox; there is no self-service
        // flow to confirm it through.
        Assert.True(admin.EmailVerified);
    }

    /// <summary>
    /// Seeding runs on every boot. It must be idempotent, and in particular must never
    /// reset the password of an account that already exists — otherwise configuration
    /// access would be account takeover.
    /// </summary>
    [Fact]
    public async Task Seeding_again_neither_duplicates_the_account_nor_changes_it()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var before = await context.Users
            .AsNoTracking()
            .SingleAsync(u => u.Email == BootstrapAdminFactory.AdminEmail);

        await scope.ServiceProvider.GetRequiredService<AdministratorSeeder>().SeedAsync();

        var after = await context.Users
            .AsNoTracking()
            .Where(u => u.Email == BootstrapAdminFactory.AdminEmail)
            .ToListAsync();

        var only = Assert.Single(after);
        Assert.Equal(before.Id, only.Id);
        Assert.Equal(before.PasswordHash, only.PasswordHash);
    }

    /// <summary>
    /// The public registration endpoint must still refuse to mint an admin, whatever the
    /// bootstrap mechanism allows internally.
    /// </summary>
    [Fact]
    public async Task Self_registration_still_cannot_create_an_admin()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"not-an-admin-{Guid.NewGuid():N}@example.com",
            password = "correct-horse-battery-staple",
            firstName = "Mallory",
            lastName = "Test",
            role = "Admin",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

/// <summary>
/// With nothing configured — the default — nothing is provisioned. A deployment that
/// forgets to set these must not silently acquire an admin account.
/// </summary>
[Collection(nameof(ApiCollection))]
public class AdministratorProvisioningDisabledTests(GoSitApiFactory factory)
{
    [Fact]
    public async Task No_administrator_is_created_when_none_is_configured()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        await scope.ServiceProvider.GetRequiredService<AdministratorSeeder>().SeedAsync();

        // The shared factory configures no bootstrap admin. Any Admin rows present are
        // ones the other suites promoted by hand, never ones the seeder created.
        var seeded = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == BootstrapAdminFactory.AdminEmail);

        Assert.False(seeded);
    }
}
