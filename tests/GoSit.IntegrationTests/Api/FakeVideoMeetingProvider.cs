using System.Collections.Concurrent;
using GoSit.Application.Abstractions;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Stands in for LiveKit.
///
/// A real LiveKit project cannot run inside the test host, and the feature under test is
/// what GoSit does around the video service — who may join, whether the recording was
/// started before a token was issued, what a webhook changes — not whether LiveKit works.
/// The fake records what was asked of it and can be told to refuse.
/// </summary>
public sealed class FakeVideoMeetingProvider : IVideoMeetingProvider
{
    private readonly ConcurrentDictionary<string, string> _recordingByRoom = new();
    private int _nextRecording;

    public bool IsConfigured => true;

    /// <summary>When set, starting a recording throws — the case where a join must be refused.</summary>
    public volatile bool RefuseToRecord;

    public ConcurrentBag<string> RoomsPrepared { get; } = [];

    public ConcurrentBag<string> RoomsClosed { get; } = [];

    public ConcurrentBag<(string Room, string StorageKey)> RecordingsStarted { get; } = [];

    /// <summary>The next webhook to hand back from verification, set by a test.</summary>
    public VideoWebhookEvent? NextWebhook { get; set; }

    public Task PrepareRoomAsync(string roomName, CancellationToken cancellationToken)
    {
        RoomsPrepared.Add(roomName);
        return Task.CompletedTask;
    }

    /// <summary>When set, every call to the service fails as if it were unreachable.</summary>
    public volatile bool Unreachable;

    public Task<bool> IsRecordingAsync(string roomName, CancellationToken cancellationToken) =>
        Unreachable
            ? throw new HttpRequestException("No such host is known.")
            : Task.FromResult(_recordingByRoom.ContainsKey(roomName));

    public Task<string> StartRecordingAsync(string roomName, string storageKey, CancellationToken cancellationToken)
    {
        if (RefuseToRecord)
        {
            throw new HttpRequestException("Egress unavailable.");
        }

        var id = $"EG_{Interlocked.Increment(ref _nextRecording)}";
        _recordingByRoom[roomName] = id;
        RecordingsStarted.Add((roomName, storageKey));

        return Task.FromResult(id);
    }

    public Task CloseRoomAsync(string roomName, CancellationToken cancellationToken)
    {
        RoomsClosed.Add(roomName);
        _recordingByRoom.TryRemove(roomName, out _);
        return Task.CompletedTask;
    }

    /// <summary>The recording the fake has running for a room, so a test can end it by webhook.</summary>
    public string? RecordingFor(string roomName) =>
        _recordingByRoom.TryGetValue(roomName, out var id) ? id : null;

    /// <summary>Simulates the recording stopping, as LiveKit would report before its webhook.</summary>
    public void StopRecording(string roomName) => _recordingByRoom.TryRemove(roomName, out _);

    public VideoJoinTicket IssueJoinTicket(
        string roomName,
        string participantIdentity,
        string displayName,
        DateTime expiresAtUtc) =>
        new("wss://fake.livekit.test", $"token:{roomName}:{participantIdentity}:{displayName}", expiresAtUtc);

    public Task<VideoWebhookEvent> VerifyWebhookAsync(string payload, string authorization, CancellationToken cancellationToken)
    {
        if (authorization != "Bearer signed-by-test")
        {
            throw new WebhookVerificationException("Not signed.");
        }

        return Task.FromResult(NextWebhook ?? new VideoWebhookEvent(VideoWebhookKind.Other, null, null, false));
    }
}

/// <summary>Recordings "stored" in memory. The video service would have written them; here a test does.</summary>
public sealed class InMemoryRecordingStore : IRecordingStore
{
    public ConcurrentDictionary<string, byte[]> Files { get; } = new();

    public ConcurrentBag<string> Deleted { get; } = [];

    public bool IsLive => true;

    public Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<StoredDocument?> ReadAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(Files.TryGetValue(key, out var bytes)
            ? new StoredDocument(new MemoryStream(bytes), "video/mp4", "interview-recording.mp4")
            : null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Files.TryRemove(key, out _);
        Deleted.Add(key);
        return Task.CompletedTask;
    }
}
