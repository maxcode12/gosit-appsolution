using System.Net;
using System.Text;
using GoSit.Application.Abstractions;
using GoSit.Domain.Notifications;

namespace GoSit.Infrastructure.Email;

/// <summary>
/// Turns a notification into an email.
///
/// Hand-rolled rather than templated through the provider. Brevo can host templates, but
/// that would put user-facing copy in a dashboard nobody reviews, outside version control
/// and outside code review — for a product where the wording of "your booking was
/// cancelled" matters, that is the wrong place for it.
///
/// <b>The content rule:</b> a notification's title and body are already written to be
/// safe on a lock screen, and this adds nothing to them. No addresses, no care needs, no
/// message content — the email says something happened and links to the app.
/// </summary>
public static class NotificationEmailTemplate
{
    public static EmailMessage Render(
        Notification notification,
        string recipientName,
        string recipientEmail,
        string webAppBaseUrl)
    {
        var link = LinkFor(notification, webAppBaseUrl.TrimEnd('/'));
        var action = ActionFor(notification.Type);

        return new EmailMessage(
            recipientEmail,
            recipientName,
            notification.Title,
            Html(notification, recipientName, link, action),
            Text(notification, recipientName, link, action));
    }

    /// <summary>
    /// Where the email points. Deep-links to the thing that happened where there is one,
    /// so someone is not dropped on a dashboard to go hunting.
    /// </summary>
    private static string LinkFor(Notification notification, string baseUrl) =>
        notification.Type switch
        {
            NotificationType.MessageReceived => $"{baseUrl}/messages",

            NotificationType.BookingRequested => $"{baseUrl}/caregiver/bookings",

            NotificationType.BookingAccepted
                or NotificationType.BookingDeclined
                or NotificationType.BookingCancelled
                or NotificationType.BookingStarting
                or NotificationType.BookingCompleted
                // A dispute reads differently for each side — the family sees their
                // complaint, the caregiver sees why their pay stopped — but both of
                // those live on the booking, so one link serves both.
                or NotificationType.BookingDisputed
                or NotificationType.DisputeResolved =>
                notification.RelatedEntityId is { } bookingId
                    ? $"{baseUrl}/bookings/{bookingId}"
                    : $"{baseUrl}/bookings",

            NotificationType.CertificationExpiring
                or NotificationType.InsuranceExpiring => $"{baseUrl}/caregiver/account",

            // One page for both sides; it works out which side is looking.
            NotificationType.InterviewRequested
                or NotificationType.InterviewAccepted
                or NotificationType.InterviewDeclined
                or NotificationType.InterviewCancelled =>
                notification.RelatedEntityId is { } interviewId
                    ? $"{baseUrl}/interviews/{interviewId}"
                    : baseUrl,

            _ => baseUrl,
        };

    private static string ActionFor(NotificationType type) => type switch
    {
        NotificationType.MessageReceived => "Read your message",
        NotificationType.BookingRequested => "View the request",
        NotificationType.CertificationExpiring => "Update your certifications",
        NotificationType.InsuranceExpiring => "Update your insurance",
        NotificationType.InterviewRequested
            or NotificationType.InterviewAccepted
            or NotificationType.InterviewDeclined
            or NotificationType.InterviewCancelled => "View the interview",
        _ => "View your booking",
    };

    /// <summary>
    /// Table-based layout with inline styles.
    ///
    /// Not how anyone would write a web page in 2026, and exactly how email still has to
    /// be written: Outlook renders with Word's engine, Gmail strips &lt;style&gt; blocks,
    /// and flexbox is unsupported across a good share of clients.
    /// </summary>
    private static string Html(
        Notification notification,
        string recipientName,
        string link,
        string action)
    {
        var body = new StringBuilder();

        body.Append(
            """
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width"></head>
            <body style="margin:0;padding:0;background:#f5f3f0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Helvetica,Arial,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f5f3f0;padding:32px 16px;">
                <tr><td align="center">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#ffffff;border-radius:16px;padding:32px;">
                    <tr><td style="padding-bottom:24px;">
                      <span style="font-size:20px;font-weight:800;color:#0d838c;letter-spacing:-0.02em;">GoSit</span>
                    </td></tr>
            """);

        body.Append(
            $"""
                    <tr><td style="font-size:20px;font-weight:700;color:#23201c;padding-bottom:8px;line-height:1.3;">
                      {Escape(notification.Title)}
                    </td></tr>
                    <tr><td style="font-size:15px;color:#6f665c;line-height:1.6;padding-bottom:24px;">
                      Hi {Escape(recipientName)},<br>{Escape(notification.Body ?? "Open GoSit for the details.")}
                    </td></tr>
                    <tr><td style="padding-bottom:28px;">
                      <a href="{Escape(link)}" style="display:inline-block;background:#0d838c;color:#ffffff;text-decoration:none;font-weight:600;font-size:15px;padding:12px 24px;border-radius:12px;">{Escape(action)}</a>
                    </td></tr>
                    <tr><td style="border-top:1px solid #eae6e1;padding-top:20px;font-size:12px;color:#746b5f;line-height:1.6;">
                      You are receiving this because you have a GoSit account.
                      Manage what we email you in your account settings.
                    </td></tr>
            """);

        body.Append(
            """
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """);

        return body.ToString();
    }

    private static string Text(
        Notification notification,
        string recipientName,
        string link,
        string action)
    {
        var body = new StringBuilder();

        body.AppendLine(notification.Title);
        body.AppendLine();
        body.AppendLine($"Hi {recipientName},");
        body.AppendLine();
        body.AppendLine(notification.Body ?? "Open GoSit for the details.");
        body.AppendLine();
        body.AppendLine($"{action}: {link}");
        body.AppendLine();
        body.AppendLine("--");
        body.AppendLine("You are receiving this because you have a GoSit account.");

        return body.ToString();
    }

    /// <summary>
    /// Escapes anything interpolated into the HTML body.
    ///
    /// Titles carry names people chose for themselves, and a name containing a bracket
    /// would otherwise break the markup — or worse, be a way to put markup into an email
    /// GoSit sends on someone else's behalf.
    /// </summary>
    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
