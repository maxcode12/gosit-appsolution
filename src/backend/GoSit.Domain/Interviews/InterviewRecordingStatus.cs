namespace GoSit.Domain.Interviews;

public enum InterviewRecordingStatus
{
    /// <summary>The video service is recording. The file does not exist yet.</summary>
    Recording = 1,

    /// <summary>Saved to storage and watchable by staff until it is deleted.</summary>
    Available = 2,

    /// <summary>
    /// The recording stopped with an error. A partial file may exist, so it keeps its
    /// storage key and is deleted on the same schedule as a finished one.
    /// </summary>
    Failed = 3,

    /// <summary>Deleted from storage. The row stays as the record that it existed.</summary>
    Deleted = 4,
}
