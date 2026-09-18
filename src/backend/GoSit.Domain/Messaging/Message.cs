using GoSit.Domain.Common;

namespace GoSit.Domain.Messaging;

/// <summary>
/// A single message in a conversation.
///
/// Message bodies are private to the two participants: never logged, never placed in a
/// notification body, never returned to anyone outside the conversation.
/// </summary>
public sealed class Message : Entity
{
    public const int MaxBodyLength = 4000;

    private Message()
    {
    }

    public Guid ConversationId { get; private set; }

    /// <summary>Profile id of the sender — a CareRequesterProfile or a CaregiverProfile.</summary>
    public Guid SenderProfileId { get; private set; }

    public bool SentByCaregiver { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTime SentAtUtc { get; private set; }

    /// <summary>
    /// Set when the body looks like an exchange of contact details. The message is
    /// delivered normally; this only surfaces it for moderation.
    /// </summary>
    public bool FlaggedForReview { get; private set; }

    /// <summary>
    /// What a staff member decided about the flag, and who decided it.
    ///
    /// A flag was raised by a regex and cleared by nobody: `ContactDetailPolicy` is
    /// deliberately loose, on the grounds that "a false positive costs a review", and
    /// there was no review to cost. Recording the outcome is what turns the flag from a
    /// column into a queue that can be emptied.
    ///
    /// The flag itself is never unset. Whether a message looked like contact sharing is a
    /// fact about the message; whether that mattered is a judgement about it, and
    /// overwriting the first with the second would lose the evidence a later safety
    /// decision may need.
    /// </summary>
    public MessageModerationOutcome? ModerationOutcome { get; private set; }

    public Guid? ModeratedByUserId { get; private set; }

    public DateTime? ModeratedAtUtc { get; private set; }

    /// <summary>Still waiting on a staff decision — the moderation queue.</summary>
    public bool AwaitsModeration => FlaggedForReview && ModerationOutcome is null;

    /// <summary>
    /// Records a staff decision about a flagged message.
    ///
    /// Deciding twice is refused rather than ignored: two staff members reaching opposite
    /// conclusions is a real event, and silently keeping the first would hide it.
    /// </summary>
    public void Moderate(MessageModerationOutcome outcome, Guid staffUserId, DateTime utcNow)
    {
        if (!FlaggedForReview)
        {
            throw new DomainException("That message was not flagged for review.");
        }

        if (ModerationOutcome is not null)
        {
            throw new DomainException("That message has already been reviewed.");
        }

        if (staffUserId == Guid.Empty)
        {
            throw new DomainException("A reviewer is required.");
        }

        ModerationOutcome = outcome;
        ModeratedByUserId = staffUserId;
        ModeratedAtUtc = utcNow;
    }

    internal static Message Create(
        Guid conversationId,
        Guid senderProfileId,
        bool sentByCaregiver,
        string body,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DomainException("A message cannot be empty.");
        }

        var trimmed = body.Trim();

        if (trimmed.Length > MaxBodyLength)
        {
            throw new DomainException($"A message must be {MaxBodyLength} characters or fewer.");
        }

        return new Message
        {
            ConversationId = conversationId,
            SenderProfileId = senderProfileId,
            SentByCaregiver = sentByCaregiver,
            Body = trimmed,
            SentAtUtc = utcNow,
            FlaggedForReview = ContactDetailPolicy.LooksLikeContactSharing(trimmed),
        };
    }
}
