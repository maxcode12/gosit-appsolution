namespace GoSit.Domain.Interviews;

/// <summary>
/// The rules of an interview's schedule and of how long its recordings are kept.
///
/// The retention period is a product decision, not a technical one: long enough for a
/// family or caregiver to raise a concern about what was said, short enough that GoSit
/// is not holding months of video of people's homes for no reason.
/// </summary>
public static class InterviewPolicy
{
    public static readonly IReadOnlyList<int> AllowedDurationsMinutes = [15, 30, 45, 60];

    /// <summary>Time for the caregiver to see the request and reply before it starts.</summary>
    public static readonly TimeSpan MinimumNotice = TimeSpan.FromHours(1);

    public static readonly TimeSpan MaximumHorizon = TimeSpan.FromDays(60);

    /// <summary>How early either side can join, to check their camera and microphone.</summary>
    public static readonly TimeSpan JoinOpensBefore = TimeSpan.FromMinutes(10);

    /// <summary>How long after the scheduled end the room stays joinable, for a late start.</summary>
    public static readonly TimeSpan JoinClosesAfterEnd = TimeSpan.FromMinutes(30);

    /// <summary>How long a recording is kept unless staff hold it.</summary>
    public static readonly TimeSpan RecordingRetention = TimeSpan.FromDays(90);

    /// <summary>
    /// How far back an accepted interview can start and still be open: the longest
    /// interview plus the time the room stays joinable after it.
    /// </summary>
    public static readonly TimeSpan LongestOpenSpan =
        TimeSpan.FromMinutes(AllowedDurationsMinutes.Max()) + JoinClosesAfterEnd;

    public const int MaxHoldReasonLength = 500;
}
