using System.Security.Cryptography;
using System.Text;
using GoSit.Application.Abstractions;
using GoSit.Infrastructure.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// The real LiveKit provider's webhook verification, against tokens built the way LiveKit
/// builds them: signed with the API secret, issued by the API key, carrying a SHA-256 of
/// the body. This is the part the fake provider skips, so it is tested on its own.
/// </summary>
public class LiveKitWebhookTests
{
    private const string ApiKey = "APIkeyfortests";
    private const string ApiSecret = "a-livekit-api-secret-that-is-long-enough-for-hmac-sha256";

    private static LiveKitVideoMeetingProvider Provider() =>
        new(
            new NoHttpClientFactory(),
            Options.Create(new LiveKitOptions { ServerUrl = "wss://tests.livekit.cloud", ApiKey = ApiKey, ApiSecret = ApiSecret }),
            Options.Create(new InterviewRecordingOptions { ConnectionString = "AccountName=x;AccountKey=y" }),
            NullLogger<LiveKitVideoMeetingProvider>.Instance);

    private static string Sign(string payload, string? secret = null, string? bodyHash = null)
    {
        var hash = bodyHash ?? Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = ApiKey,
            Claims = new Dictionary<string, object> { ["sha256"] = hash },
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret ?? ApiSecret)), SecurityAlgorithms.HmacSha256),
        });
    }

    private const string EgressEnded = """
        {"event":"egress_ended","egressInfo":{"egressId":"EG_abc","roomName":"interview-0123","status":"EGRESS_COMPLETE"}}
        """;

    [Fact]
    public async Task A_correctly_signed_webhook_is_read()
    {
        var received = await Provider().VerifyWebhookAsync(EgressEnded, $"Bearer {Sign(EgressEnded)}", CancellationToken.None);

        Assert.Equal(VideoWebhookKind.RecordingEnded, received.Kind);
        Assert.Equal("EG_abc", received.ProviderRecordingId);
        Assert.Equal("interview-0123", received.RoomName);
        Assert.True(received.RecordingSucceeded);
    }

    [Fact]
    public async Task A_webhook_signed_with_the_wrong_secret_is_refused()
    {
        await Assert.ThrowsAsync<WebhookVerificationException>(() =>
            Provider().VerifyWebhookAsync(EgressEnded, $"Bearer {Sign(EgressEnded, secret: "not-the-secret-not-the-secret-not-the-secret")}", CancellationToken.None));
    }

    [Fact]
    public async Task A_valid_token_over_a_different_body_is_refused()
    {
        var token = Sign(EgressEnded);
        var tampered = EgressEnded.Replace("EGRESS_COMPLETE", "EGRESS_FAILED");

        await Assert.ThrowsAsync<WebhookVerificationException>(() =>
            Provider().VerifyWebhookAsync(tampered, $"Bearer {token}", CancellationToken.None));
    }

    [Fact]
    public async Task No_signature_is_refused()
    {
        await Assert.ThrowsAsync<WebhookVerificationException>(() =>
            Provider().VerifyWebhookAsync(EgressEnded, string.Empty, CancellationToken.None));
    }

    [Theory]
    [InlineData("EGRESS_COMPLETE", true)]
    [InlineData("EGRESS_LIMIT_REACHED", true)]
    [InlineData("EGRESS_FAILED", false)]
    [InlineData("EGRESS_ABORTED", false)]
    public void Only_a_recording_that_left_a_file_counts_as_saved(string status, bool succeeded)
    {
        var payload = $$$"""{"event":"egress_ended","egress_info":{"egress_id":"EG_1","room_name":"r","status":"{{{status}}}"}}""";

        Assert.Equal(succeeded, LiveKitVideoMeetingProvider.Parse(payload).RecordingSucceeded);
    }

    [Fact]
    public void A_closed_room_is_read_by_name()
    {
        var received = LiveKitVideoMeetingProvider.Parse("""{"event":"room_finished","room":{"name":"interview-42"}}""");

        Assert.Equal(VideoWebhookKind.RoomFinished, received.Kind);
        Assert.Equal("interview-42", received.RoomName);
    }

    [Fact]
    public void Events_interviews_do_not_care_about_are_ignored()
    {
        Assert.Equal(VideoWebhookKind.Other, LiveKitVideoMeetingProvider.Parse("""{"event":"participant_joined"}""").Kind);
    }

    private sealed class NoHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("No HTTP in this test.");
    }
}
