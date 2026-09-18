using System.Net;
using System.Net.Http.Json;
using GoSit.Application.Features.Authentication.AccountRecovery;
using GoSit.Domain.Users;
using GoSit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Captures the recovery emails instead of sending them, keeping the tokens so a test
/// can click the link the way a person would.
/// </summary>
public sealed class RecordingRecoveryNotifier : IAccountRecoveryNotifier
{
    public List<(string Email, string Token)> Verifications { get; } = [];

    public List<(string Email, string Token)> Resets { get; } = [];

    public List<string> PasswordChanged { get; } = [];

    public Task SendEmailVerificationAsync(
        string email, string firstName, string token, CancellationToken cancellationToken)
    {
        Verifications.Add((email, token));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(
        string email, string firstName, string token, CancellationToken cancellationToken)
    {
        Resets.Add((email, token));
        return Task.CompletedTask;
    }

    public Task SendPasswordChangedAsync(
        string email, string firstName, CancellationToken cancellationToken)
    {
        PasswordChanged.Add(email);
        return Task.CompletedTask;
    }
}

public sealed class RecoveryFactoryFixture : GoSitApiFactory
{
    public RecordingRecoveryNotifier Notifier { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.AddScoped<IAccountRecoveryNotifier>(_ => Notifier);
        });
    }
}

/// <summary>
/// Email verification and password reset, driven through HTTP the way a person would.
///
/// The security properties are what these are for. A reset flow that works is the easy
/// half; one that cannot be used to enumerate accounts, cannot be replayed, and cannot
/// leave a live key behind is the half that gets skipped.
/// </summary>
public class AccountRecoveryTests(RecoveryFactoryFixture factory)
    : IClassFixture<RecoveryFactoryFixture>
{
    private const string Password = "correct-horse-battery-staple";

    private async Task<(HttpClient Client, string Email)> RegisterAsync()
    {
        var email = $"recovery-{Guid.NewGuid():N}@example.com";
        var client = factory.CreateClient();

        var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = Password,
            firstName = "Ada",
            lastName = "Lovelace",
            role = "CareRequester",
        });

        registered.EnsureSuccessStatusCode();
        var tokens = await registered.Content.ReadFromJsonAsync<ProfileTestClient.Tokens>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        return (client, email);
    }

    private async Task<bool> IsVerifiedAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.Users
            .AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => u.EmailVerified)
            .SingleAsync();
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    // ---- Email verification ------------------------------------------------

    [Fact]
    public async Task A_new_account_starts_unverified_and_can_verify_by_link()
    {
        var (client, email) = await RegisterAsync();

        Assert.False(await IsVerifiedAsync(email));

        factory.Notifier.Verifications.Clear();

        (await client.PostAsync("/api/v1/auth/email-verification", null))
            .EnsureSuccessStatusCode();

        var (_, token) = Assert.Single(factory.Notifier.Verifications, v => v.Email == email);

        var confirmed = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/email-verification/confirm", new { token });

        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        Assert.True(await IsVerifiedAsync(email));
    }

    /// <summary>A verification link is spent once. A forwarded email is not a spare key.</summary>
    [Fact]
    public async Task A_verification_link_cannot_be_used_twice()
    {
        var (client, email) = await RegisterAsync();

        factory.Notifier.Verifications.Clear();
        (await client.PostAsync("/api/v1/auth/email-verification", null))
            .EnsureSuccessStatusCode();

        var (_, token) = Assert.Single(factory.Notifier.Verifications, v => v.Email == email);

        var anonymous = factory.CreateClient();

        (await anonymous.PostAsJsonAsync(
            "/api/v1/auth/email-verification/confirm", new { token })).EnsureSuccessStatusCode();

        var replayed = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/email-verification/confirm", new { token });

        Assert.Equal(HttpStatusCode.BadRequest, replayed.StatusCode);
    }

    /// <summary>
    /// Asking again supersedes the first link. Otherwise every request would add another
    /// working key to the account.
    /// </summary>
    [Fact]
    public async Task Asking_again_invalidates_the_previous_link()
    {
        var (client, email) = await RegisterAsync();

        factory.Notifier.Verifications.Clear();

        (await client.PostAsync("/api/v1/auth/email-verification", null)).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/v1/auth/email-verification", null)).EnsureSuccessStatusCode();

        var issued = factory.Notifier.Verifications.Where(v => v.Email == email).ToList();
        Assert.Equal(2, issued.Count);

        var anonymous = factory.CreateClient();

        var first = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/email-verification/confirm", new { token = issued[0].Token });

        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);

        var second = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/email-verification/confirm", new { token = issued[1].Token });

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task Requesting_verification_requires_being_signed_in()
    {
        var response = await factory.CreateClient()
            .PostAsync("/api/v1/auth/email-verification", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Password reset ----------------------------------------------------

    [Fact]
    public async Task A_reset_link_sets_a_new_password_that_works()
    {
        var (_, email) = await RegisterAsync();
        var anonymous = factory.CreateClient();

        factory.Notifier.Resets.Clear();

        (await anonymous.PostAsJsonAsync("/api/v1/auth/password-reset", new { email }))
            .EnsureSuccessStatusCode();

        var (_, token) = Assert.Single(factory.Notifier.Resets, r => r.Email == email);

        const string newPassword = "a-completely-different-passphrase";

        var reset = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm", new { token, newPassword });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        (await LoginAsync(factory.CreateClient(), email, newPassword)).EnsureSuccessStatusCode();

        var old = await LoginAsync(factory.CreateClient(), email, Password);
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
    }

    /// <summary>
    /// The enumeration defence. An unknown address must be indistinguishable from a real
    /// one, or the endpoint becomes a way to find out who has a GoSit account.
    /// </summary>
    [Fact]
    public async Task Requesting_a_reset_says_nothing_about_whether_the_account_exists()
    {
        var (_, realEmail) = await RegisterAsync();
        var anonymous = factory.CreateClient();

        var known = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/password-reset", new { email = realEmail });

        var unknown = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/password-reset",
            new { email = $"nobody-{Guid.NewGuid():N}@example.com" });

        Assert.Equal(HttpStatusCode.NoContent, known.StatusCode);
        Assert.Equal(unknown.StatusCode, known.StatusCode);
    }

    [Fact]
    public async Task A_reset_link_cannot_be_used_twice()
    {
        var (_, email) = await RegisterAsync();
        var anonymous = factory.CreateClient();

        factory.Notifier.Resets.Clear();
        (await anonymous.PostAsJsonAsync("/api/v1/auth/password-reset", new { email }))
            .EnsureSuccessStatusCode();

        var (_, token) = Assert.Single(factory.Notifier.Resets, r => r.Email == email);

        (await anonymous.PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm",
            new { token, newPassword = "first-new-passphrase-here" })).EnsureSuccessStatusCode();

        var replayed = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm",
            new { token, newPassword = "second-new-passphrase-here" });

        Assert.Equal(HttpStatusCode.BadRequest, replayed.StatusCode);
    }

    /// <summary>
    /// Resetting signs the account out everywhere. Whoever prompted the reset — often
    /// someone who thinks their account is compromised — needs the intruder's session
    /// gone, not just the password changed.
    /// </summary>
    [Fact]
    public async Task Resetting_signs_out_every_existing_session()
    {
        var (client, email) = await RegisterAsync();

        // Prove the existing session works before the reset.
        (await client.GetAsync("/api/v1/me")).EnsureSuccessStatusCode();

        var anonymous = factory.CreateClient();

        factory.Notifier.Resets.Clear();
        (await anonymous.PostAsJsonAsync("/api/v1/auth/password-reset", new { email }))
            .EnsureSuccessStatusCode();

        var (_, token) = Assert.Single(factory.Notifier.Resets, r => r.Email == email);

        (await anonymous.PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm",
            new { token, newPassword = "yet-another-good-passphrase" })).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var live = await context.Users
            .AsNoTracking()
            .Where(u => u.Email == email)
            .SelectMany(u => u.RefreshTokens)
            .CountAsync(t => t.RevokedAtUtc == null);

        Assert.Equal(0, live);
    }

    /// <summary>
    /// Someone who redeems a reset link has proved they control the mailbox, so the
    /// address is verified as a side effect rather than by a second link.
    /// </summary>
    [Fact]
    public async Task Resetting_also_verifies_the_address()
    {
        var (_, email) = await RegisterAsync();
        var anonymous = factory.CreateClient();

        Assert.False(await IsVerifiedAsync(email));

        factory.Notifier.Resets.Clear();
        (await anonymous.PostAsJsonAsync("/api/v1/auth/password-reset", new { email }))
            .EnsureSuccessStatusCode();

        var (_, token) = Assert.Single(factory.Notifier.Resets, r => r.Email == email);

        (await anonymous.PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm",
            new { token, newPassword = "verified-by-resetting-it" })).EnsureSuccessStatusCode();

        Assert.True(await IsVerifiedAsync(email));
    }

    /// <summary>Their warning, if the reset was not them.</summary>
    [Fact]
    public async Task A_completed_reset_tells_the_account_holder()
    {
        var (_, email) = await RegisterAsync();
        var anonymous = factory.CreateClient();

        factory.Notifier.Resets.Clear();
        factory.Notifier.PasswordChanged.Clear();

        (await anonymous.PostAsJsonAsync("/api/v1/auth/password-reset", new { email }))
            .EnsureSuccessStatusCode();

        var (_, token) = Assert.Single(factory.Notifier.Resets, r => r.Email == email);

        (await anonymous.PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm",
            new { token, newPassword = "changed-and-they-are-told" })).EnsureSuccessStatusCode();

        Assert.Contains(email, factory.Notifier.PasswordChanged);
    }

    [Fact]
    public async Task A_made_up_token_is_refused()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm",
            new { token = "not-a-real-token", newPassword = "a-perfectly-fine-passphrase" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>A reset must not be a way around the password policy.</summary>
    [Fact]
    public async Task A_reset_still_enforces_the_password_rules()
    {
        var (_, email) = await RegisterAsync();
        var anonymous = factory.CreateClient();

        factory.Notifier.Resets.Clear();
        (await anonymous.PostAsJsonAsync("/api/v1/auth/password-reset", new { email }))
            .EnsureSuccessStatusCode();

        var (_, token) = Assert.Single(factory.Notifier.Resets, r => r.Email == email);

        var response = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm", new { token, newPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // And the token survives, so a rejected attempt has not cost them their link.
        (await LoginAsync(factory.CreateClient(), email, Password)).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// A verification token must not work as a password reset. Purpose is part of the
    /// lookup precisely so the two cannot be crossed.
    /// </summary>
    [Fact]
    public async Task A_verification_token_cannot_be_used_to_reset_a_password()
    {
        var (client, email) = await RegisterAsync();

        factory.Notifier.Verifications.Clear();
        (await client.PostAsync("/api/v1/auth/email-verification", null))
            .EnsureSuccessStatusCode();

        var (_, token) = Assert.Single(factory.Notifier.Verifications, v => v.Email == email);

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/password-reset/confirm",
            new { token, newPassword = "trying-to-cross-the-streams" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Only the hash is stored, so a database leak does not hand an attacker a working
    /// set of reset links.
    /// </summary>
    [Fact]
    public async Task The_plaintext_token_is_never_stored()
    {
        var (_, email) = await RegisterAsync();
        var anonymous = factory.CreateClient();

        factory.Notifier.Resets.Clear();
        (await anonymous.PostAsJsonAsync("/api/v1/auth/password-reset", new { email }))
            .EnsureSuccessStatusCode();

        var (_, token) = Assert.Single(factory.Notifier.Resets, r => r.Email == email);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var stored = await context.UserTokens
            .AsNoTracking()
            .Where(t => t.Purpose == UserTokenPurpose.PasswordReset)
            .Select(t => t.TokenHash)
            .ToListAsync();

        Assert.DoesNotContain(token, stored);
    }
}
