using GoSit.Domain.Common;

namespace GoSit.Domain.Notifications;

/// <summary>
/// What a notification is about. Used by clients to choose an icon and a destination,
/// and by the delivery layer to decide whether email or push is warranted.
/// </summary>
public enum NotificationType
{
    MessageReceived = 1,
    BookingRequested = 2,
    BookingAccepted = 3,
    BookingDeclined = 4,
    BookingCancelled = 5,
    BookingStarting = 6,
    BookingCompleted = 7,
    CertificationExpiring = 8,

    /// <summary>A booking has been disputed, which also stops the caregiver's payout.</summary>
    BookingDisputed = 9,

    /// <summary>Staff have settled a dispute. Carries the decision, not the reasoning.</summary>
    DisputeResolved = 10,

    InterviewRequested = 11,
    InterviewAccepted = 12,
    InterviewDeclined = 13,
    InterviewCancelled = 14,

    /// <summary>An approved insurance policy is expiring soon, or has lapsed.</summary>
    InsuranceExpiring = 15,
}

/// <summary>
/// An in-app notification for one account.
///
/// The body is a short, non-sensitive summary — "Bo N. sent you a message", never the
/// message itself. Notifications are delivered by email and push, which land on lock
/// screens and in mailboxes, so they must never carry care needs, addresses, message
/// content, or anything else private.
/// </summary>
public sealed class Notification : Entity
{
    private const int MaxTitleLength = 200;
    private const int MaxBodyLength = 500;

    private Notification()
    {
    }

    public Guid UserId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Short summary. Safe to show on a lock screen.</summary>
    public string? Body { get; private set; }

    /// <summary>What to open — for example "CareBooking" and the booking id.</summary>
    public string? RelatedEntityType { get; private set; }

    public Guid? RelatedEntityId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc is not null;

    public static Notification Create(
        Guid userId,
        NotificationType type,
        string title,
        string? body,
        string? relatedEntityType,
        Guid? relatedEntityId,
        DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A recipient is required.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("A notification title is required.");
        }

        return new Notification
        {
            UserId = userId,
            Type = type,
            Title = Truncate(title.Trim(), MaxTitleLength),
            Body = string.IsNullOrWhiteSpace(body) ? null : Truncate(body.Trim(), MaxBodyLength),
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            CreatedAtUtc = utcNow,
        };
    }

    public void MarkRead(DateTime utcNow) => ReadAtUtc ??= utcNow;

    /*
     * ---- Email delivery ----------------------------------------------------
     *
     * Tracked on the notification rather than in a separate queue, because the
     * notification *is* the thing to be delivered. A second table would be a second
     * source of truth about whether someone was told their booking was cancelled.
     */

    /// <summary>When the email went out. Null while it is still owed.</summary>
    public DateTime? EmailSentAtUtc { get; private set; }

    /// <summary>
    /// Set when there is no email provider, so nothing was sent and nothing ever will
    /// be for this one.
    ///
    /// Distinct from <see cref="EmailSentAtUtc"/> on purpose: "we did not send this"
    /// and "we sent this" must never be the same state, or the record would claim
    /// someone was told something they were not.
    /// </summary>
    public bool EmailSuppressed { get; private set; }

    public int EmailAttemptCount { get; private set; }

    public DateTime? LastEmailAttemptAtUtc { get; private set; }

    /// <summary>
    /// Backoff between delivery attempts, doubling and capped.
    ///
    /// Shorter than the payout backoff: a mail relay hiccup clears in minutes, and a
    /// booking notification an hour late has already missed its moment.
    /// </summary>
    private static readonly TimeSpan[] RetryBackoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(3),
    ];

    /// <summary>
    /// How many times to try before giving up.
    ///
    /// Unlike a payout, an email is not owed forever — a notification nobody could be
    /// told about after two days of trying is stale, and the in-app notification is
    /// still there regardless.
    /// </summary>
    private const int MaxEmailAttempts = 8;

    /// <summary>Whether the dispatcher should try to send this now.</summary>
    public bool IsEmailDue(DateTime utcNow)
    {
        if (EmailSentAtUtc is not null || EmailSuppressed)
        {
            return false;
        }

        if (EmailAttemptCount >= MaxEmailAttempts)
        {
            return false;
        }

        return LastEmailAttemptAtUtc is not { } last
            || utcNow - last >= BackoffFor(EmailAttemptCount);
    }

    private static TimeSpan BackoffFor(int attempts) =>
        RetryBackoff[Math.Min(Math.Max(attempts - 1, 0), RetryBackoff.Length - 1)];

    /// <summary>
    /// Recorded before the send is attempted, so a send that fails by exception backs
    /// off exactly like one that fails by response.
    /// </summary>
    public void RecordEmailAttempt(DateTime utcNow)
    {
        EmailAttemptCount++;
        LastEmailAttemptAtUtc = utcNow;
    }

    public void MarkEmailSent(DateTime utcNow) => EmailSentAtUtc ??= utcNow;

    /// <summary>No provider, or no address to send to. Nothing was delivered.</summary>
    public void SuppressEmail() => EmailSuppressed = true;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
