using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>A host started the way a deployment starts: sign-up closed, one invited tester.</summary>
public sealed class ClosedRegistrationFactory : GoSitApiFactory
{
    public const string InvitedEmail = "invited.tester@example.com";

    protected override IDictionary<string, string?> SettingOverrides =>
        new Dictionary<string, string?>
        {
            ["Registration:Open"] = "false",
            // Mixed case and a second address on purpose: the list is not case sensitive and
            // may hold several, separated by commas and spaces.
            ["Registration:InvitedEmails"] = $"someone.else@example.com, {InvitedEmail.ToUpperInvariant()}",
        };
}

/// <summary>
/// Sign-up stays shut until it is deliberately opened.
///
/// A deployed API is on the open internet from its first minute. Without this, whoever found
/// the address first could create accounts before the legal documents, insurance and
/// screening behind them exist.
/// </summary>
public class ClosedRegistrationTests : IClassFixture<ClosedRegistrationFactory>
{
    private const string Password = "a-long-enough-test-password-1";

    private readonly ClosedRegistrationFactory _factory;

    public ClosedRegistrationTests(ClosedRegistrationFactory factory) => _factory = factory;

    private Task<HttpResponseMessage> RegisterAsync(string email, string role = "CareRequester") =>
        _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = Password,
            firstName = "Ada",
            lastName = "Lovelace",
            role,
        });

    [Theory]
    [InlineData("CareRequester")]
    [InlineData("Caregiver")]
    public async Task An_address_that_is_not_invited_cannot_register(string role)
    {
        var email = $"stranger.{Guid.NewGuid():N}@example.com";

        var response = await RegisterAsync(email, role);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // And nothing was created: the same credentials do not work.
        var login = await _factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task The_refusal_explains_that_sign_up_is_not_open_yet()
    {
        var response = await RegisterAsync($"stranger.{Guid.NewGuid():N}@example.com");

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("not open yet", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_invited_address_can_register_whatever_its_capitalisation()
    {
        var response = await RegisterAsync(ClosedRegistrationFactory.InvitedEmail);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_second_address_on_the_invite_list_is_also_let_in()
    {
        var response = await RegisterAsync("Someone.Else@Example.com", "Caregiver");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Signing_in_is_unaffected_by_closed_sign_up()
    {
        var email = ClosedRegistrationFactory.InvitedEmail.Replace("invited", "returning");

        // Not invited, so it cannot register on this host; signing in simply fails on
        // credentials the way it always does, rather than on the sign-up lock.
        var login = await _factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
