using GoSit.Domain.Interviews;

namespace GoSit.Application.Abstractions;

public interface IInterviewRepository
{
    /// <summary>Loads an interview with its recordings. Callers must check participation.</summary>
    Task<Interview?> FindByIdAsync(Guid interviewId, CancellationToken cancellationToken);

    /// <summary>The interview a video-service recording belongs to, for its webhooks.</summary>
    Task<Interview?> FindByProviderRecordingIdAsync(string providerRecordingId, CancellationToken cancellationToken);

    Task<Interview?> FindByRecordingIdAsync(Guid recordingId, CancellationToken cancellationToken);

    /// <summary>One participant's interviews, newest first, optionally only those with one other person.</summary>
    Task<IReadOnlyList<Interview>> ListForParticipantAsync(
        Guid profileId,
        bool isCaregiver,
        Guid? otherProfileId,
        CancellationToken cancellationToken);

    /// <summary>Whether this pair already has an interview waiting on a reply or still to happen.</summary>
    Task<bool> HasOpenInterviewAsync(
        Guid careRequesterProfileId,
        Guid caregiverProfileId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    /// <summary>
    /// Claims the right to start this interview's recording until <paramref name="leaseUntilUtc"/>.
    /// A single conditional update, so of two people joining at once exactly one wins.
    /// </summary>
    Task<bool> TryClaimRecordingLeaseAsync(
        Guid interviewId,
        DateTime utcNow,
        DateTime leaseUntilUtc,
        CancellationToken cancellationToken);

    Task ReleaseRecordingLeaseAsync(Guid interviewId, CancellationToken cancellationToken);

    /// <summary>Interviews that have at least one recording, newest first, for staff.</summary>
    Task<IReadOnlyList<Interview>> ListRecordedAsync(int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<Interview>> ListWithRecordingsDueForDeletionAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken);

    void Add(Interview interview);
}

/// <summary>What a participant needs to connect to a video room.</summary>
public sealed record VideoJoinTicket(string ServerUrl, string Token, DateTime ExpiresAtUtc);

public enum VideoWebhookKind
{
    Other = 0,
    RecordingEnded = 1,
    RoomFinished = 2,
}

public sealed record VideoWebhookEvent(
    VideoWebhookKind Kind,
    string? RoomName,
    string? ProviderRecordingId,
    bool RecordingSucceeded);

/// <summary>
/// The video service interviews run on. Implemented over LiveKit.
///
/// Rooms, recordings and join tokens only. Nothing about who the participants are goes
/// to the provider beyond an opaque identity and the display name the other person sees.
/// </summary>
public interface IVideoMeetingProvider
{
    /// <summary>False when no video service, or nowhere to store recordings, is configured.</summary>
    bool IsConfigured { get; }

    Task PrepareRoomAsync(string roomName, CancellationToken cancellationToken);

    Task<bool> IsRecordingAsync(string roomName, CancellationToken cancellationToken);

    /// <summary>Starts recording the room to <paramref name="storageKey"/>. Returns the provider's recording id.</summary>
    Task<string> StartRecordingAsync(string roomName, string storageKey, CancellationToken cancellationToken);

    /// <summary>Ends the call for everyone in the room.</summary>
    Task CloseRoomAsync(string roomName, CancellationToken cancellationToken);

    VideoJoinTicket IssueJoinTicket(
        string roomName,
        string participantIdentity,
        string displayName,
        DateTime expiresAtUtc);

    /// <summary>Verifies a webhook's signature and reads it. Throws <see cref="WebhookVerificationException"/>.</summary>
    Task<VideoWebhookEvent> VerifyWebhookAsync(
        string payload,
        string authorization,
        CancellationToken cancellationToken);
}

/// <summary>Where interview recordings are kept. Written by the video service; read and deleted here.</summary>
public interface IRecordingStore
{
    bool IsLive { get; }

    /// <summary>Makes sure the private container exists before the video service uploads to it.</summary>
    Task PrepareAsync(CancellationToken cancellationToken);

    Task<StoredDocument?> ReadAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
