using GoSit.Domain.Common;
using GoSit.Domain.Messaging;
using GoSit.Domain.Messaging.Events;
using Xunit;

namespace GoSit.UnitTests.Domain;

public class ConversationTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Caregiver = Guid.NewGuid();

    private static Conversation Start() =>
        Conversation.Start(Requester, Caregiver, null, Now);

    [Fact]
    public void A_new_conversation_has_no_messages_and_no_last_message_time()
    {
        var conversation = Start();

        Assert.Empty(conversation.Messages);
        Assert.Null(conversation.LastMessageAtUtc);
        Assert.Equal(0, conversation.UnreadCountFor(Requester));
        Assert.Equal(0, conversation.UnreadCountFor(Caregiver));
    }

    [Fact]
    public void Both_participants_are_recognised_and_nobody_else_is()
    {
        var conversation = Start();

        Assert.True(conversation.IsParticipant(Requester));
        Assert.True(conversation.IsParticipant(Caregiver));
        Assert.False(conversation.IsParticipant(Guid.NewGuid()));
    }

    /// <summary>
    /// A private thread must not be writable by anyone outside it.
    /// </summary>
    [Fact]
    public void Someone_outside_the_conversation_cannot_send_read_or_count()
    {
        var conversation = Start();
        var stranger = Guid.NewGuid();

        Assert.Throws<DomainException>(() => conversation.SendMessage(stranger, "Hello", Now));
        Assert.Throws<DomainException>(() => conversation.MarkRead(stranger, Now));
        Assert.Throws<DomainException>(() => conversation.UnreadCountFor(stranger));
    }

    [Fact]
    public void Sending_a_message_records_the_sender_and_the_time()
    {
        var conversation = Start();

        var message = conversation.SendMessage(Requester, "  Are you free Friday?  ", Now);

        Assert.Equal("Are you free Friday?", message.Body);
        Assert.Equal(Requester, message.SenderProfileId);
        Assert.False(message.SentByCaregiver);
        Assert.Equal(Now, conversation.LastMessageAtUtc);
    }

    [Fact]
    public void Sending_raises_a_message_sent_event_addressed_to_the_other_participant()
    {
        var conversation = Start();

        conversation.SendMessage(Requester, "Are you free Friday?", Now);

        var raised = Assert.Single(conversation.DomainEvents);
        var sent = Assert.IsType<MessageSentDomainEvent>(raised);
        Assert.Equal(Requester, sent.SenderProfileId);
        Assert.Equal(Caregiver, sent.RecipientProfileId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_message_is_rejected(string body)
    {
        var conversation = Start();

        Assert.Throws<DomainException>(() => conversation.SendMessage(Requester, body, Now));
    }

    [Fact]
    public void An_over_long_message_is_rejected()
    {
        var conversation = Start();

        Assert.Throws<DomainException>(() =>
            conversation.SendMessage(Requester, new string('x', Message.MaxBodyLength + 1), Now));
    }

    [Fact]
    public void A_message_from_the_other_party_is_unread_until_opened()
    {
        var conversation = Start();

        conversation.SendMessage(Requester, "Are you free Friday?", Now);

        Assert.Equal(1, conversation.UnreadCountFor(Caregiver));
        // Your own message is never unread for you.
        Assert.Equal(0, conversation.UnreadCountFor(Requester));
    }

    [Fact]
    public void Marking_read_clears_the_unread_count()
    {
        var conversation = Start();
        conversation.SendMessage(Requester, "Are you free Friday?", Now);

        conversation.MarkRead(Caregiver, Now.AddMinutes(1));

        Assert.Equal(0, conversation.UnreadCountFor(Caregiver));
    }

    [Fact]
    public void Messages_arriving_after_a_read_are_unread_again()
    {
        var conversation = Start();
        conversation.SendMessage(Requester, "First", Now);
        conversation.MarkRead(Caregiver, Now.AddMinutes(1));

        conversation.SendMessage(Requester, "Second", Now.AddMinutes(2));

        Assert.Equal(1, conversation.UnreadCountFor(Caregiver));
    }

    /// <summary>Replying is proof of having seen the thread.</summary>
    [Fact]
    public void Replying_clears_the_senders_own_unread_count()
    {
        var conversation = Start();
        conversation.SendMessage(Requester, "Are you free Friday?", Now);

        conversation.SendMessage(Caregiver, "Yes, I am.", Now.AddMinutes(5));

        Assert.Equal(0, conversation.UnreadCountFor(Caregiver));
        Assert.Equal(1, conversation.UnreadCountFor(Requester));
    }

    [Fact]
    public void Unread_counts_are_tracked_independently_per_participant()
    {
        var conversation = Start();
        conversation.SendMessage(Requester, "One", Now);
        conversation.SendMessage(Requester, "Two", Now.AddMinutes(1));
        conversation.SendMessage(Caregiver, "Reply", Now.AddMinutes(2));

        Assert.Equal(0, conversation.UnreadCountFor(Caregiver));
        Assert.Equal(1, conversation.UnreadCountFor(Requester));
    }

    [Fact]
    public void A_conversation_can_record_the_booking_that_started_it()
    {
        var bookingId = Guid.NewGuid();

        var conversation = Conversation.Start(Requester, Caregiver, bookingId, Now);

        Assert.Equal(bookingId, conversation.InitiatedByBookingId);
    }

    [Fact]
    public void Both_participants_are_required()
    {
        Assert.Throws<DomainException>(() =>
            Conversation.Start(Guid.Empty, Caregiver, null, Now));
        Assert.Throws<DomainException>(() =>
            Conversation.Start(Requester, Guid.Empty, null, Now));
    }
}

public class ContactDetailPolicyTests
{
    /// <summary>
    /// Taking care off-platform loses the audit trail, the verified identity, and the
    /// payment protection, so these are surfaced for review.
    /// </summary>
    [Theory]
    [InlineData("Call me on 416-555-0142")]
    [InlineData("my number is 4165550142")]
    [InlineData("reach me at +1 (416) 555 0142")]
    [InlineData("email me at ada@example.com")]
    [InlineData("ada (at) example (dot) com")]
    public void Contact_details_are_flagged(string body)
    {
        Assert.True(ContactDetailPolicy.LooksLikeContactSharing(body));
    }

    [Theory]
    [InlineData("Are you free Friday at 6pm?")]
    [InlineData("Sam is 8 years old and goes to bed at 8.")]
    [InlineData("I charge 30 per hour.")]
    [InlineData("See you on the 21st!")]
    public void Ordinary_messages_are_not_flagged(string body)
    {
        Assert.False(ContactDetailPolicy.LooksLikeContactSharing(body));
    }

    /// <summary>A flagged message is still delivered — flagging is not censorship.</summary>
    [Fact]
    public void A_flagged_message_is_still_sent()
    {
        var conversation = Conversation.Start(Guid.NewGuid(), Guid.NewGuid(), null, DateTime.UtcNow);
        var sender = conversation.CareRequesterProfileId;

        var message = conversation.SendMessage(sender, "Call me on 416-555-0142", DateTime.UtcNow);

        Assert.True(message.FlaggedForReview);
        Assert.Equal("Call me on 416-555-0142", message.Body);
        Assert.Single(conversation.Messages);
    }
}
