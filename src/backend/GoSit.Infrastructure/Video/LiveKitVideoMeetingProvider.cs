using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GoSit.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GoSit.Infrastructure.Video;

/// <summary>
/// Interviews on LiveKit.
///
/// Talks to LiveKit's server API directly — it is Twirp, which is JSON over HTTP POST with
/// a short-lived signed token — rather than through a third-party SDK, so the few calls Go
/// Sit makes are all visible here and the test suite can stand in for the server.
///
/// Recordings are room composites written straight to GoSit's own Azure container: the
/// video never passes through the API, and LiveKit keeps nothing once the upload finishes.
/// </summary>
public sealed class LiveKitVideoMeetingProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<LiveKitOptions> options,
    IOptions<InterviewRecordingOptions> recordingOptions,
    ILogger<LiveKitVideoMeetingProvider> logger) : IVideoMeetingProvider
{
    public const string HttpClientName = "LiveKit";

    /// <summary>How long a server-to-server token lives. One call each.</summary>
    private static readonly TimeSpan ServerTokenLifetime = TimeSpan.FromMinutes(5);

    private static readonly JsonWebTokenHandler Tokens = new();

    public bool IsConfigured => true;

    public async Task PrepareRoomAsync(string roomName, CancellationToken cancellationToken)
    {
        // Idempotent: creating a room that exists returns it. The room closes a minute
        // after the last person leaves, which is also what ends its recording.
        using var _ = await CallAsync(
            "livekit.RoomService",
            "CreateRoom",
            new { name = roomName, empty_timeout = 300, departure_timeout = 60, max_participants = 3 },
            new Dictionary<string, object> { ["roomCreate"] = true },
            cancellationToken);
    }

    public async Task<bool> IsRecordingAsync(string roomName, CancellationToken cancellationToken)
    {
        using var document = await CallAsync(
            "livekit.Egress",
            "ListEgress",
            new { room_name = roomName, active = true },
            new Dictionary<string, object> { ["roomRecord"] = true },
            cancellationToken);

        // Protobuf's JSON leaves an empty list out entirely.
        return document.RootElement.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array
            && items.GetArrayLength() > 0;
    }

    public async Task<string> StartRecordingAsync(
        string roomName,
        string storageKey,
        CancellationToken cancellationToken)
    {
        var storage = AzureStorageCredentials.Parse(recordingOptions.Value.ConnectionString);

        using var document = await CallAsync(
            "livekit.Egress",
            "StartRoomCompositeEgress",
            new
            {
                room_name = roomName,
                layout = "grid",
                file_outputs = new[]
                {
                    new
                    {
                        file_type = "MP4",
                        filepath = storageKey,
                        azure = new
                        {
                            account_name = storage.AccountName,
                            account_key = storage.AccountKey,
                            container_name = recordingOptions.Value.ContainerName,
                        },
                    },
                },
            },
            new Dictionary<string, object> { ["roomRecord"] = true },
            cancellationToken);

        return ReadString(document.RootElement, "egress_id", "egressId")
            ?? throw new InvalidOperationException("LiveKit did not return a recording id.");
    }

    public async Task CloseRoomAsync(string roomName, CancellationToken cancellationToken)
    {
        using var _ = await CallAsync(
            "livekit.RoomService",
            "DeleteRoom",
            new { room = roomName },
            new Dictionary<string, object> { ["roomCreate"] = true },
            cancellationToken);
    }

    public VideoJoinTicket IssueJoinTicket(
        string roomName,
        string participantIdentity,
        string displayName,
        DateTime expiresAtUtc)
    {
        var grant = new Dictionary<string, object>
        {
            // This room only.
            ["room"] = roomName,
            ["roomJoin"] = true,
            ["canPublish"] = true,
            ["canSubscribe"] = true,

            // Data is only a nudge to reload the chat; the messages themselves go through
            // GoSit's messaging, where they are kept and moderated.
            ["canPublishData"] = true,

            // Camera and microphone, not screen sharing: an interview is two people talking,
            // and a shared screen is an easy way to put a phone number or an outside link
            // in front of someone.
            ["canPublishSources"] = new[] { "camera", "microphone" },
        };

        return new VideoJoinTicket(
            options.Value.ServerUrl,
            CreateToken(grant, participantIdentity, displayName, expiresAtUtc),
            expiresAtUtc);
    }

    public async Task<VideoWebhookEvent> VerifyWebhookAsync(
        string payload,
        string authorization,
        CancellationToken cancellationToken)
    {
        var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..]
            : authorization;

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new WebhookVerificationException("The webhook carried no signature.");
        }

        var result = await Tokens.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = options.Value.ApiKey,
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = SigningKey(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromMinutes(5),
        });

        if (!result.IsValid)
        {
            throw new WebhookVerificationException("The webhook signature is not valid.");
        }

        // The token signs a hash of the body, which is what ties it to this payload rather
        // than to any request that happens to carry a valid token.
        if (!result.Claims.TryGetValue("sha256", out var claimed) || claimed is not string claimedHash)
        {
            throw new WebhookVerificationException("The webhook signature does not cover its body.");
        }

        var actualHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(actualHash), Encoding.ASCII.GetBytes(claimedHash)))
        {
            throw new WebhookVerificationException("The webhook body does not match its signature.");
        }

        try
        {
            return Parse(payload);
        }
        catch (JsonException)
        {
            throw new WebhookVerificationException("The webhook body is not valid JSON.");
        }
    }

    /// <summary>
    /// Reads the events interviews care about. Field names are accepted in both of the
    /// spellings protobuf's JSON allows, so a change in how LiveKit serialises does not
    /// quietly stop recordings being marked as saved.
    /// </summary>
    public static VideoWebhookEvent Parse(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        switch (ReadString(root, "event"))
        {
            case "egress_ended":
            {
                if (Property(root, "egressInfo", "egress_info") is not { } info)
                {
                    break;
                }

                return new VideoWebhookEvent(
                    VideoWebhookKind.RecordingEnded,
                    ReadString(info, "roomName", "room_name"),
                    ReadString(info, "egressId", "egress_id"),
                    RecordingSucceeded(info));
            }

            case "room_finished":
            {
                var room = Property(root, "room");

                return new VideoWebhookEvent(
                    VideoWebhookKind.RoomFinished,
                    room is { } r ? ReadString(r, "name") : null,
                    null,
                    false);
            }
        }

        return new VideoWebhookEvent(VideoWebhookKind.Other, null, null, false);
    }

    /// <summary>
    /// Complete, or stopped at the service's length limit — both leave a file. Anything
    /// else (failed, aborted) does not count as a saved recording.
    /// </summary>
    private static bool RecordingSucceeded(JsonElement info)
    {
        if (Property(info, "status") is not { } status)
        {
            return false;
        }

        return status.ValueKind switch
        {
            JsonValueKind.String => status.GetString() is "EGRESS_COMPLETE" or "EGRESS_LIMIT_REACHED",
            JsonValueKind.Number => status.GetInt32() is 3 or 6,
            _ => false,
        };
    }

    private async Task<JsonDocument> CallAsync(
        string service,
        string method,
        object body,
        Dictionary<string, object> grant,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl()}/twirp/{service}/{method}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(grant, identity: null, name: null, DateTime.UtcNow + ServerTokenLifetime));

        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // The status only. LiveKit's error body can echo the request, and the request
            // carries storage credentials.
            logger.LogWarning("LiveKit {Method} failed with status {StatusCode}", method, (int)response.StatusCode);

            throw new HttpRequestException(
                $"LiveKit {method} failed with status {(int)response.StatusCode}.", null, response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private string CreateToken(
        Dictionary<string, object> videoGrant,
        string? identity,
        string? name,
        DateTime expiresAtUtc)
    {
        var claims = new Dictionary<string, object> { ["video"] = videoGrant };

        if (identity is not null)
        {
            claims["sub"] = identity;
        }

        if (name is not null)
        {
            claims["name"] = name;
        }

        var utcNow = DateTime.UtcNow;

        return Tokens.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Value.ApiKey,
            Claims = claims,
            IssuedAt = utcNow,
            NotBefore = utcNow,
            Expires = expiresAtUtc,
            SigningCredentials = new SigningCredentials(SigningKey(), SecurityAlgorithms.HmacSha256),
        });
    }

    private SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(options.Value.ApiSecret));

    /// <summary>The server API lives on the same host as the room connection, over HTTPS.</summary>
    private string ApiBaseUrl()
    {
        var url = options.Value.ServerUrl.TrimEnd('/');

        if (url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            return "https://" + url["wss://".Length..];
        }

        return url.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
            ? "http://" + url["ws://".Length..]
            : url;
    }

    private static JsonElement? Property(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement element, params string[] names) =>
        Property(element, names) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
}

/// <summary>
/// What runs when LiveKit or recording storage is not configured.
///
/// Refuses rather than pretending, for the reason document storage does: every interview
/// is recorded, so a call with no video service or nowhere to keep the recording cannot
/// go ahead. Requests for interviews are refused before anyone arranges one.
/// </summary>
public sealed class UnconfiguredVideoMeetingProvider : IVideoMeetingProvider
{
    public bool IsConfigured => false;

    public Task PrepareRoomAsync(string roomName, CancellationToken cancellationToken) => throw NotConfigured();

    public Task<bool> IsRecordingAsync(string roomName, CancellationToken cancellationToken) => throw NotConfigured();

    public Task<string> StartRecordingAsync(string roomName, string storageKey, CancellationToken cancellationToken) =>
        throw NotConfigured();

    public Task CloseRoomAsync(string roomName, CancellationToken cancellationToken) => throw NotConfigured();

    public VideoJoinTicket IssueJoinTicket(
        string roomName,
        string participantIdentity,
        string displayName,
        DateTime expiresAtUtc) =>
        throw NotConfigured();

    public Task<VideoWebhookEvent> VerifyWebhookAsync(
        string payload,
        string authorization,
        CancellationToken cancellationToken) =>
        throw new WebhookVerificationException("Video interviews are not configured.");

    private static InvalidOperationException NotConfigured() =>
        new("Video interviews are not configured.");
}
