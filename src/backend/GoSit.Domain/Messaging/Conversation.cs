using GoSit.Domain.Common;
using GoSit.Domain.Messaging.Events;

namespace GoSit.Domain.Messaging;

/// <summary>
/// A private thread between one care requester and one caregiver.
///
/// There is a single conversation per pair rather than one per booking, so the history
/// with a caregiver stays in one place across repeat bookings. A booking can start a
/// conversation — recorded in <see cref="InitiatedByBookingId"/> for context — but does
/// not own it.
///
/// Only the two participants may read or write. That is enforced here rather than in a
/// handler, so no route can bypass it.
/// </summary>
public sealed class Conversation : Entity
{
    private readonly List<Message> _messages = [];

    private Conversation()
    {
    }

    public Guid CareRequesterProfileId { get; private set; }

    public Guid CaregiverProfileId { get; private set; }

    /// <summary>The booking that prompted this conversation, if any.</summary>
    public Guid? InitiatedByBookingId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>When the most recent message arrived. Null until someone writes.</summary>
    public DateTime? LastMessageAtUtc { get; private set; }

    /// <summary>
    /// Read state is a timestamp per participant rather than a flag per message: it is
    /// one value to update when someone opens a thread, and unread counts fall out of it.
    /// </summary>
    public DateTime? CareRequesterLastReadAtUtc { get; private set; }

    public DateTime? CaregiverLastReadAtUtc { get; private set; }

    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    /// <summary>
    /// Started by the care requester only.
    ///
    /// A caregiver cannot open a thread with someone who has not contacted them, which
    /// keeps the marketplace from becoming a channel for unsolicited approaches to
    /// families. Caregivers reply to conversations that already exist.
    /// </summary>
    public static Conversation Start(
        Guid careRequesterProfileId,
        Guid caregiverProfileId,
        Guid? initiatedByBookingId,
        DateTime utcNow)
    {
        if (careRequesterProfileId == Guid.Empty || caregiverProfileId == Guid.Empty)
        {
            throw new DomainException("Both participants are required.");
        }

        return new Conversation
        {
            CareRequesterProfileId = careRequesterProfileId,
            CaregiverProfileId = caregiverProfileId,
            InitiatedByBookingId = initiatedByBookingId,
            CreatedAtUtc = utcNow,
        };
    }

    public bool IsParticipant(Guid profileId) =>
        profileId == CareRequesterProfileId || profileId == CaregiverProfileId;

    public Message SendMessage(Guid senderProfileId, string body, DateTime utcNow)
    {
        if (!IsParticipant(senderProfileId))
        {
            throw new DomainException("Only a participant can send a message.");
        }

        var sentByCaregiver = senderProfileId == CaregiverProfileId;

        var message = Message.Create(Id, senderProfileId, sentByCaregiver, body, utcNow);
        _messages.Add(message);
        LastMessageAtUtc = utcNow;

        // Sending is itself a read: a participant has clearly seen the thread.
        if (sentByCaregiver)
        {
            CaregiverLastReadAtUtc = utcNow;
        }
        else
        {
            CareRequesterLastReadAtUtc = utcNow;
        }

        RaiseDomainEvent(new MessageSentDomainEvent(
            Id,
            message.Id,
            senderProfileId,
            RecipientOf(senderProfileId),
            message.FlaggedForReview,
            utcNow));

        return message;
    }

    public void MarkRead(Guid profileId, DateTime utcNow)
    {
        if (!IsParticipant(profileId))
        {
            throw new DomainException("Only a participant can read this conversation.");
        }

        if (profileId == CaregiverProfileId)
        {
            CaregiverLastReadAtUtc = utcNow;
        }
        else
        {
            CareRequesterLastReadAtUtc = utcNow;
        }
    }

    /// <summary>
    /// Messages sent by the other participant since this one last read. Own messages
    /// never count as unread.
    /// </summary>
    public int UnreadCountFor(Guid profileId)
    {
        if (!IsParticipant(profileId))
        {
            throw new DomainException("Only a participant can read this conversation.");
        }

        var isCaregiver = profileId == CaregiverProfileId;
        var lastRead = isCaregiver ? CaregiverLastReadAtUtc : CareRequesterLastReadAtUtc;

        return _messages.Count(m =>
            m.SentByCaregiver != isCaregiver
            && (lastRead is null || m.SentAtUtc > lastRead));
    }

    public Guid RecipientOf(Guid senderProfileId) =>
        senderProfileId == CaregiverProfileId ? CareRequesterProfileId : CaregiverProfileId;
}
