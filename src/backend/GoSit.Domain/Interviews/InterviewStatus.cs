namespace GoSit.Domain.Interviews;

/// <summary>
/// Where an interview stands. "Expired" and "Missed" are not stored: they are what a
/// request nobody answered, or an accepted interview nobody joined, look like once the
/// time has passed, and are worked out when an interview is read.
/// </summary>
public enum InterviewStatus
{
    Requested = 1,
    Accepted = 2,
    Declined = 3,
    Cancelled = 4,

    /// <summary>The call happened: someone joined and the room has since closed.</summary>
    Completed = 5,
}
