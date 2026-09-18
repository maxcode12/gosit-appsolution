using System.Net;
using GoSit.Application.Abstractions;
using GoSit.Application.Features.Authentication.AccountRecovery;
using GoSit.Infrastructure.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoSit.Infrastructure.Email;

/// <summary>
/// The account-recovery emails.
///
/// Sent inline rather than through the notification dispatcher: someone watching a
/// mailbox for a reset link should not be waiting on a 30-second poll.
///
/// <b>These are the only emails GoSit sends that carry a secret.</b> The token is in the
/// link and nowhere else — not in the database, not in a log line, not in an audit row.
/// A send that fails is logged without it.
/// </summary>
public sealed class AccountRecoveryNotifier(
    IEmailSender sender,
    IOptions<WebAppOptions> webApp,
    ILogger<AccountRecoveryNotifier> logger) : IAccountRecoveryNotifier
{
    private string BaseUrl => webApp.Value.BaseUrl.TrimEnd('/');

    public Task SendEmailVerificationAsync(
        string email, string firstName, string token, CancellationToken cancellationToken)
    {
        var link = $"{BaseUrl}/verify-email?token={WebUtility.UrlEncode(token)}";

        return SendAsync(
            new EmailMessage(
                email,
                firstName,
                "Confirm your email address",
                Layout(
                    "Confirm your email address",
                    firstName,
                    "Tap below to confirm this is your address. It keeps your account "
                    + "recoverable if you ever forget your password.",
                    link,
                    "Confirm my email",
                    "This link works for seven days."),
                Text(
                    "Confirm your email address",
                    firstName,
                    "Confirm this is your address so your account stays recoverable.",
                    link,
                    "This link works for seven days.")),
            cancellationToken);
    }

    public Task SendPasswordResetAsync(
        string email, string firstName, string token, CancellationToken cancellationToken)
    {
        var link = $"{BaseUrl}/reset-password?token={WebUtility.UrlEncode(token)}";

        return SendAsync(
            new EmailMessage(
                email,
                firstName,
                "Reset your GoSit password",
                Layout(
                    "Reset your password",
                    firstName,
                    "Tap below to choose a new password. If you did not ask for this, "
                    + "you can ignore this email — nothing has changed, and your current "
                    + "password still works.",
                    link,
                    "Choose a new password",
                    "This link works for one hour and can only be used once."),
                Text(
                    "Reset your password",
                    firstName,
                    "Choose a new password using the link below. If you did not ask for "
                    + "this, ignore this email — nothing has changed.",
                    link,
                    "This link works for one hour and can only be used once.")),
            cancellationToken);
    }

    /// <summary>
    /// Sent after the fact, and not optional.
    ///
    /// If the reset was not them, this is how they find out — which is why it goes even
    /// though the password has already changed and there is nothing they can click to
    /// undo it. It tells them to contact support, because that is the real remedy.
    /// </summary>
    public Task SendPasswordChangedAsync(
        string email, string firstName, CancellationToken cancellationToken) =>
        SendAsync(
            new EmailMessage(
                email,
                firstName,
                "Your GoSit password was changed",
                Layout(
                    "Your password was changed",
                    firstName,
                    "Your password has just been changed and you have been signed out on "
                    + "every device. If this was you, there is nothing to do. If it was "
                    + "not, contact us straight away.",
                    $"{BaseUrl}/login",
                    "Log in",
                    "You are receiving this because someone changed the password on your account."),
                Text(
                    "Your password was changed",
                    firstName,
                    "Your password has just been changed and you have been signed out "
                    + "everywhere. If this was not you, contact us straight away.",
                    $"{BaseUrl}/login",
                    "You are receiving this because someone changed the password on your account.")),
            cancellationToken);

    private async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!sender.IsLive)
        {
            // Loud, because a recovery email that silently goes nowhere leaves someone
            // locked out with no explanation. The token is not logged.
            logger.LogWarning(
                "No email provider is configured — \"{Subject}\" was not sent",
                message.Subject);

            return;
        }

        await sender.SendAsync(message, cancellationToken);

        logger.LogInformation("Sent an account email: {Subject}", message.Subject);
    }

    /// <summary>
    /// Table layout with inline styles, for the same reason as the notification
    /// template: Outlook renders with Word's engine and Gmail strips style blocks.
    /// </summary>
    private static string Layout(
        string heading,
        string firstName,
        string body,
        string link,
        string action,
        string footnote) =>
        $"""
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
                <tr><td style="font-size:20px;font-weight:700;color:#23201c;padding-bottom:8px;line-height:1.3;">
                  {Escape(heading)}
                </td></tr>
                <tr><td style="font-size:15px;color:#6f665c;line-height:1.6;padding-bottom:24px;">
                  Hi {Escape(firstName)},<br>{Escape(body)}
                </td></tr>
                <tr><td style="padding-bottom:20px;">
                  <a href="{Escape(link)}" style="display:inline-block;background:#0d838c;color:#ffffff;text-decoration:none;font-weight:600;font-size:15px;padding:12px 24px;border-radius:12px;">{Escape(action)}</a>
                </td></tr>
                <tr><td style="font-size:13px;color:#746b5f;line-height:1.6;padding-bottom:20px;">
                  {Escape(footnote)}
                </td></tr>
                <tr><td style="border-top:1px solid #eae6e1;padding-top:20px;font-size:12px;color:#746b5f;line-height:1.6;word-break:break-all;">
                  If the button does not work, paste this into your browser:<br>{Escape(link)}
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string Text(
        string heading, string firstName, string body, string link, string footnote) =>
        $"""
        {heading}

        Hi {firstName},

        {body}

        {link}

        {footnote}
        """;

    /// <summary>
    /// Escapes everything interpolated. First names are user-supplied, and a name
    /// containing a bracket would otherwise break the markup — or be a way to put markup
    /// into an email GoSit sends.
    /// </summary>
    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
