using GoSit.Domain.Notifications;
using Xunit;

namespace GoSit.UnitTests.Notifications;

/// <summary>
/// When a notification email should be attempted, and what each outcome records.
///
/// The rule underneath all of these: <b>"we did not send this" and "we sent this" must
/// never collapse into the same state.</b> The record of whether someone was told their
/// booking was cancelled is the kind of thing that gets read back during a complaint,
/// and a system that guesses optimistically would be worse than one that sends nothing.
/// </summary>
public class NotificationEmailDeliveryTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Notification Pending() =>
        Notification.Create(
            Guid.NewGuid(),
            NotificationType.BookingAccepted,
            "Priya confirmed your booking",
            "Open GoSit for the details.",
            "CareBooking",
            Guid.NewGuid(),
            Now);

    [Fact]
    public void A_new_notification_is_due_immediately()
    {
        Assert.True(Pending().IsEmailDue(Now));
    }

    [Fact]
    public void A_sent_notification_is_never_sent_again()
    {
        var notification = Pending();
        notification.RecordEmailAttempt(Now);
        notification.MarkEmailSent(Now);

        Assert.False(notification.IsEmailDue(Now.AddDays(7)));
    }

    /// <summary>
    /// Marking sent is idempotent, and keeps the first timestamp. A duplicate delivery
    /// must not rewrite when someone was actually told.
    /// </summary>
    [Fact]
    public void Marking_sent_twice_keeps_the_first_time()
    {
        var notification = Pending();
        notification.MarkEmailSent(Now);
        notification.MarkEmailSent(Now.AddHours(3));

        Assert.Equal(Now, notification.EmailSentAtUtc);
    }

    /// <summary>
    /// The honesty rule. With no provider there is nothing to send, and the record says
    /// so — suppressed, not sent.
    /// </summary>
    [Fact]
    public void A_suppressed_notification_is_not_recorded_as_sent()
    {
        var notification = Pending();
        notification.SuppressEmail();

        Assert.True(notification.EmailSuppressed);
        Assert.Null(notification.EmailSentAtUtc);
        Assert.False(notification.IsEmailDue(Now));
    }

    [Fact]
    public void A_failed_attempt_waits_before_retrying()
    {
        var notification = Pending();
        notification.RecordEmailAttempt(Now);

        Assert.False(notification.IsEmailDue(Now.AddSeconds(59)));
        Assert.True(notification.IsEmailDue(Now.AddMinutes(1)));
    }

    [Fact]
    public void The_wait_grows_with_each_failure()
    {
        var notification = Pending();

        notification.RecordEmailAttempt(Now);
        notification.RecordEmailAttempt(Now.AddMinutes(1));

        // Second failure: five minutes.
        Assert.False(notification.IsEmailDue(Now.AddMinutes(5)));
        Assert.True(notification.IsEmailDue(Now.AddMinutes(6)));
    }

    /// <summary>
    /// Unlike a payout, an email is not owed forever. A notification nobody could be
    /// told about after repeated tries is stale — and the in-app notification is still
    /// there regardless, so nothing is actually lost.
    /// </summary>
    [Fact]
    public void Delivery_eventually_gives_up()
    {
        var notification = Pending();

        for (var attempt = 0; attempt < 8; attempt++)
        {
            notification.RecordEmailAttempt(Now.AddHours(attempt * 6));
        }

        Assert.False(notification.IsEmailDue(Now.AddDays(30)));
        Assert.Null(notification.EmailSentAtUtc);
    }

    /// <summary>
    /// Giving up is not the same as suppressing, and neither is the same as sending.
    /// Three distinguishable outcomes, because they mean three different things when
    /// someone asks what happened.
    /// </summary>
    [Fact]
    public void Giving_up_is_distinguishable_from_suppressing_and_from_sending()
    {
        var exhausted = Pending();

        for (var attempt = 0; attempt < 8; attempt++)
        {
            exhausted.RecordEmailAttempt(Now);
        }

        Assert.False(exhausted.EmailSuppressed);
        Assert.Null(exhausted.EmailSentAtUtc);
        Assert.Equal(8, exhausted.EmailAttemptCount);
    }
}
