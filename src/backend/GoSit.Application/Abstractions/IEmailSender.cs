namespace GoSit.Application.Abstractions;

/// <summary>
/// Sending one email.
///
/// Deliberately narrow: a recipient, a subject, and two bodies. Anything richer —
/// templates, campaigns, scheduling — belongs to the provider, not to GoSit's domain,
/// and putting it behind this interface would tie the application layer to whichever
/// provider is in use this year.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Whether a real provider is configured. With none, the caller records what it
    /// would have sent rather than pretending delivery happened.
    /// </summary>
    bool IsLive { get; }

    /// <summary>
    /// Sends, or throws. Callers must treat a throw as "not delivered, try again" —
    /// the dispatcher relies on that to decide what to retry.
    /// </summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// One email.
///
/// Both a plain-text and an HTML body, always. Text-only clients still exist, spam
/// filters score multipart messages better, and a plain-text alternative is the only
/// version some screen readers will read aloud.
///
/// <b>The bodies must carry no sensitive personal information.</b> Email lands in
/// mailboxes GoSit does not control and on lock screens in public — so a notification
/// email says "you have a new message", never the message; "your booking is confirmed",
/// never the address or the care needs. See <c>Notification</c>, which is built to the
/// same rule.
/// </summary>
public sealed record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody);
