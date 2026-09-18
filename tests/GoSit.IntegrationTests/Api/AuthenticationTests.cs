using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace GoSit.IntegrationTests.Api;

[Collection(nameof(ApiCollection))]
public class AuthenticationTests(GoSitApiFactory factory)
{
    private const string ValidPassword = "correct-horse-battery-staple";

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private sealed record Tokens(
        string AccessToken,
        DateTime AccessTokenExpiresAtUtc,
        string RefreshToken,
        Guid UserId,
        string Role);

    private sealed record ProblemBody(string? Title, int? Status);

    private sealed record MeResponse(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        string Role,
        bool EmailVerified);

    private async Task<(Tokens Tokens, string Email)> RegisterAsync(string role = "CareRequester")
    {
        var email = UniqueEmail();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = ValidPassword,
            firstName = "Ada",
            lastName = "Lovelace",
            role,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<Tokens>();
        Assert.NotNull(tokens);
        return (tokens, email);
    }

    [Fact]
    public async Task Register_returns_a_usable_token_pair()
    {
        var (tokens, _) = await RegisterAsync();

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal("CareRequester", tokens.Role);
        Assert.NotEqual(Guid.Empty, tokens.UserId);
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email()
    {
        var (_, email) = await RegisterAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = ValidPassword,
            firstName = "Grace",
            lastName = "Hopper",
            role = "CareRequester",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email_differing_only_by_case()
    {
        var (_, email) = await RegisterAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = email.ToUpperInvariant(),
            password = ValidPassword,
            firstName = "Grace",
            lastName = "Hopper",
            role = "CareRequester",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", ValidPassword)]
    [InlineData("valid@example.com", "short")]
    public async Task Register_rejects_invalid_input(string email, string password)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password,
            firstName = "Ada",
            lastName = "Lovelace",
            role = "CareRequester",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_refuses_to_create_an_admin_account()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = UniqueEmail(),
            password = ValidPassword,
            firstName = "Mallory",
            lastName = "Admin",
            role = "Admin",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_succeeds_with_correct_credentials()
    {
        var (_, email) = await RegisterAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ValidPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<Tokens>();
        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
    }

    [Fact]
    public async Task Login_is_case_insensitive_on_email()
    {
        var (_, email) = await RegisterAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = email.ToUpperInvariant(),
            password = ValidPassword,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_rejects_a_wrong_password()
    {
        var (_, email) = await RegisterAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "wrong-password-entirely" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// An unknown email and a wrong password must be indistinguishable, otherwise the
    /// endpoint can be used to discover which addresses have accounts.
    /// </summary>
    [Fact]
    public async Task Login_does_not_reveal_whether_an_account_exists()
    {
        var (_, email) = await RegisterAsync();
        var client = factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "wrong-password-entirely" });
        var unknownAccount = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = UniqueEmail(), password = ValidPassword });

        Assert.Equal(wrongPassword.StatusCode, unknownAccount.StatusCode);

        // Compare the problem details rather than the raw bodies: every response carries
        // its own correlation id, which is expected to differ.
        var wrongPasswordProblem = await wrongPassword.Content.ReadFromJsonAsync<ProblemBody>();
        var unknownAccountProblem = await unknownAccount.Content.ReadFromJsonAsync<ProblemBody>();

        Assert.NotNull(wrongPasswordProblem);
        Assert.Equal(wrongPasswordProblem.Title, unknownAccountProblem?.Title);
        Assert.Equal(wrongPasswordProblem.Status, unknownAccountProblem?.Status);
    }

    [Fact]
    public async Task Refresh_issues_a_new_pair_and_retires_the_old_token()
    {
        var (tokens, _) = await RegisterAsync();
        var client = factory.CreateClient();

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        var newTokens = await refreshed.Content.ReadFromJsonAsync<Tokens>();
        Assert.NotNull(newTokens);
        Assert.NotEqual(tokens.RefreshToken, newTokens.RefreshToken);

        // The retired token must no longer work.
        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    /// <summary>
    /// Replaying a rotated token is treated as theft: every session is revoked, so
    /// the token the attacker's victim currently holds stops working too.
    /// </summary>
    [Fact]
    public async Task Replaying_a_rotated_token_revokes_the_whole_session_family()
    {
        var (tokens, _) = await RegisterAsync();
        var client = factory.CreateClient();

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });
        var newTokens = await refreshed.Content.ReadFromJsonAsync<Tokens>();
        Assert.NotNull(newTokens);

        await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken });

        var afterReplay = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = newTokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReplay.StatusCode);
    }

    [Fact]
    public async Task Refresh_rejects_an_unknown_token()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = "definitely-not-a-real-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var (tokens, _) = await RegisterAsync();
        var client = factory.CreateClient();

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout",
            new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var afterLogout = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_of_an_unknown_token_still_succeeds()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/logout",
            new { refreshToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_authenticated_account()
    {
        var (tokens, email) = await RegisterAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(me);
        Assert.Equal(tokens.UserId, me.Id);
        Assert.Equal(email, me.Email);
        Assert.Equal("CareRequester", me.Role);
        Assert.False(me.EmailVerified);
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_rejects_a_token_signed_with_the_wrong_key()
    {
        var client = factory.CreateClient();
        // Structurally a JWT, but not signed by this server.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.invalid-signature");

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Caregiver_registration_carries_the_caregiver_role()
    {
        var (tokens, _) = await RegisterAsync("Caregiver");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var me = await client.GetFromJsonAsync<MeResponse>("/api/v1/me");

        Assert.NotNull(me);
        Assert.Equal("Caregiver", me.Role);
    }

    /// <summary>
    /// The password must never come back out of the API in any form.
    /// </summary>
    [Fact]
    public async Task Responses_never_include_credential_material()
    {
        var (tokens, _) = await RegisterAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var body = await client.GetStringAsync("/api/v1/me");

        Assert.DoesNotContain(ValidPassword, body);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
    }
}
