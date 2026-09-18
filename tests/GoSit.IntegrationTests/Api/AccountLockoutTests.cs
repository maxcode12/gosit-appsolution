using System.Net;
using System.Net.Http.Json;
using GoSit.Domain.Users;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Per-account lockout, through the endpoint.
///
/// The unit tests cover the arithmetic. What only shows up here is whether the endpoint
/// gives the lockout away — a login that answered differently for a locked account than
/// for an unknown one would turn the protection into an account-enumeration oracle, which
/// is a worse leak than the guessing it prevents.
/// </summary>
[Collection(nameof(ApiCollection))]
public class AccountLockoutTests(GoSitApiFactory factory)
{
    private const string CorrectPassword = "correct-horse-battery-staple";

    private sealed record ProblemBody(string? Title);

    /// <summary>A registered account, and the email it was registered with.</summary>
    private async Task<string> RegisterAsync()
    {
        var email = $"lockout-{Guid.NewGuid():N}@example.com";

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = CorrectPassword,
            firstName = "Ada",
            lastName = "Lovelace",
            role = "CareRequester",
        });

        response.EnsureSuccessStatusCode();

        return email;
    }

    private static Task<HttpResponseMessage> SignInAsync(
        HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    /// <summary>
    /// Spends the tolerance.
    ///
    /// A fresh client each time, because the API rate-limits per IP and the point of this
    /// feature is the attack that rate limiting does not stop. The test factory raises
    /// that limit; this only mirrors what a distributed guess would look like.
    /// </summary>
    private async Task ExhaustAttemptsAsync(string email)
    {
        for (var i = 0; i < User.MaximumFailedAttempts; i++)
        {
            using var response = await SignInAsync(
                factory.CreateClient(), email, $"wrong-password-{i}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task The_correct_password_is_refused_once_the_account_is_locked()
    {
        var email = await RegisterAsync();

        // It works before the lockout, which is what makes the refusal afterwards mean
        // something rather than being a broken password.
        using (var before = await SignInAsync(factory.CreateClient(), email, CorrectPassword))
        {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        }

        await ExhaustAttemptsAsync(email);

        using var after = await SignInAsync(factory.CreateClient(), email, CorrectPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    /// <summary>
    /// The leak worth engineering against. A locked account, a wrong password on a real
    /// account, and an address that was never registered must be indistinguishable —
    /// otherwise the lockout becomes a way to discover who has a GoSit account, and for
    /// a caregiver or a family that is itself sensitive.
    /// </summary>
    [Fact]
    public async Task A_locked_account_is_indistinguishable_from_an_unknown_one()
    {
        var email = await RegisterAsync();
        await ExhaustAttemptsAsync(email);

        using var locked = await SignInAsync(factory.CreateClient(), email, CorrectPassword);
        using var unknown = await SignInAsync(
            factory.CreateClient(), $"nobody-{Guid.NewGuid():N}@example.com", CorrectPassword);

        Assert.Equal(unknown.StatusCode, locked.StatusCode);

        var lockedBody = await locked.Content.ReadFromJsonAsync<ProblemBody>();
        var unknownBody = await unknown.Content.ReadFromJsonAsync<ProblemBody>();

        Assert.Equal(unknownBody!.Title, lockedBody!.Title);
    }

    /// <summary>
    /// One account's lockout must not touch another's. Sharing the counter — by IP, say —
    /// would let one attacker lock out every family behind a shared connection.
    /// </summary>
    [Fact]
    public async Task Locking_one_account_does_not_lock_another()
    {
        var locked = await RegisterAsync();
        var untouched = await RegisterAsync();

        await ExhaustAttemptsAsync(locked);

        using var response = await SignInAsync(factory.CreateClient(), untouched, CorrectPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Signing in successfully before the tolerance runs out resets it, so a person who
    /// mistypes a few times and then gets in does not sit one slip away from a lockout.
    /// </summary>
    [Fact]
    public async Task A_successful_sign_in_resets_the_count()
    {
        var email = await RegisterAsync();

        for (var i = 0; i < User.MaximumFailedAttempts - 1; i++)
        {
            using var failed = await SignInAsync(factory.CreateClient(), email, "wrong");
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        using (var success = await SignInAsync(factory.CreateClient(), email, CorrectPassword))
        {
            Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        }

        Assert.Equal(0, await FailedAttemptsAsync(email));
    }

    /// <summary>
    /// The route back for someone who is locked out and — correctly — told nothing about
    /// why. Making them serve the timer as well would punish the person who did the right
    /// thing, when control of the mailbox is stronger evidence than the password was.
    /// </summary>
    [Fact]
    public async Task A_password_reset_clears_the_lockout()
    {
        var email = await RegisterAsync();
        await ExhaustAttemptsAsync(email);

        var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/v1/auth/password-reset", new { email }))
            .EnsureSuccessStatusCode();

        var token = await IssuedResetTokenAsync(email);

        var reset = await client.PostAsJsonAsync("/api/v1/auth/password-reset/confirm", new
        {
            token,
            newPassword = "an-entirely-different-passphrase",
        });

        reset.EnsureSuccessStatusCode();

        using var signIn = await SignInAsync(
            factory.CreateClient(), email, "an-entirely-different-passphrase");

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    private async Task<int> FailedAttemptsAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var normalized = User.Normalize(email);

        return await context.Users
            .AsNoTracking()
            .Where(u => u.NormalizedEmail == normalized)
            .Select(u => u.FailedLoginAttempts)
            .SingleAsync();
    }

    /// <summary>
    /// Only the hash of a reset token is stored, so the plaintext cannot be read back.
    /// The test issues its own against the same account instead — the lockout is what is
    /// under test, not the token plumbing, which AccountRecoveryTests covers.
    /// </summary>
    private async Task<string> IssuedResetTokenAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<
            GoSit.Application.Abstractions.ITokenService>();

        var normalized = User.Normalize(email);

        var user = await context.Users
            .Include(u => u.Tokens)
            .SingleAsync(u => u.NormalizedEmail == normalized);

        var issued = tokens.CreateRefreshToken();

        user.IssueToken(
            UserTokenPurpose.PasswordReset,
            issued.Hash,
            DateTime.UtcNow,
            TimeSpan.FromHours(1));

        await context.SaveChangesAsync();

        return issued.Value;
    }
}
