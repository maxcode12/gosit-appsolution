namespace GoSit.Domain.Messaging;

/// <summary>
/// What a staff member concluded about a message flagged for contact sharing.
///
/// There are only two answers because there are only two things moderation can do here.
/// The message is already delivered and is never edited or withdrawn — censoring what two
/// adults said to each other after the fact is not a power this queue has. What it decides
/// is whether the flag warranted anything.
/// </summary>
public enum MessageModerationOutcome
{
    /// <summary>
    /// Nothing wrong. The expected outcome for most of the queue: the pattern matches ten
    /// digits in a row, which is also a postal code beside a house number, a booking
    /// reference, or a date typed without spaces.
    /// </summary>
    NoConcern = 1,

    /// <summary>
    /// Escalated to an incident report about the sender, which is where an actual
    /// decision about someone's account gets made and recorded.
    /// </summary>
    Escalated = 2,
}
