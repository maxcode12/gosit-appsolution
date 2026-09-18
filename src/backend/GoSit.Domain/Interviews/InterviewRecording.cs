using GoSit.Domain.Common;

namespace GoSit.Domain.Interviews;

/// <summary>
/// One recording of an interview's call.
///
/// An interview can have more than one: if both people leave and come back, or a
/// recording fails and the call is restarted, each stretch of the call is its own file.
/// </summary>
public sealed class InterviewRecording : Entity
{
    private InterviewRecording()
    {
    }

    public Guid InterviewId { get; private set; }

    /// <summary>The video service's id for the recording, used to match its webhooks.</summary>
    public string ProviderRecordingId { get; private set; } = string.Empty;

    /// <summary>Where the file is kept. Null once it has been deleted.</summary>
    public string? StorageKey { get; private set; }

    public InterviewRecordingStatus Status { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? EndedAtUtc { get; private set; }

    /// <summary>When the retention sweep deletes the file, unless the interview is on hold.</summary>
    public DateTime? DeleteAfterUtc { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>Whether staff can watch it: finished, saved, and not yet deleted.</summary>
    public bool IsWatchable => Status == InterviewRecordingStatus.Available && StorageKey is not null;

    public bool IsDueForDeletion(DateTime utcNow) =>
        Status is InterviewRecordingStatus.Available or InterviewRecordingStatus.Failed
        && StorageKey is not null
        && DeleteAfterUtc <= utcNow;

    internal static InterviewRecording Start(
        Guid interviewId,
        string providerRecordingId,
        string storageKey,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(providerRecordingId) || string.IsNullOrWhiteSpace(storageKey))
        {
            throw new DomainException("A recording needs an id and a storage location.");
        }

        return new InterviewRecording
        {
            InterviewId = interviewId,
            ProviderRecordingId = providerRecordingId,
            StorageKey = storageKey,
            Status = InterviewRecordingStatus.Recording,
            StartedAtUtc = utcNow,
        };
    }

    internal void Finish(bool succeeded, DateTime utcNow)
    {
        EndedAtUtc = utcNow;
        Status = succeeded ? InterviewRecordingStatus.Available : InterviewRecordingStatus.Failed;

        // A failed recording is deleted on the same clock: any partial file is still video
        // of someone, and it is not something staff can watch.
        DeleteAfterUtc = utcNow + InterviewPolicy.RecordingRetention;
    }

    internal void MarkDeleted(DateTime utcNow)
    {
        Status = InterviewRecordingStatus.Deleted;
        StorageKey = null;
        DeletedAtUtc = utcNow;
    }
}
