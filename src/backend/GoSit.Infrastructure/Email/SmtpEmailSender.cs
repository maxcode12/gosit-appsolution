using GoSit.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GoSit.Infrastructure.Email;

/// <summary>
/// Sends through an SMTP relay, currently Brevo.
///
/// MailKit rather than <c>System.Net.Mail.SmtpClient</c>, which Microsoft's own
/// documentation says not to use for new work — it does not handle modern TLS
/// negotiation properly and cannot be made to.
///
/// A connection per message. Pooling would be faster, but the dispatcher sends a handful
/// of emails a minute and a long-lived authenticated SMTP connection is a thing to
/// monitor, reconnect and get wrong. Not worth it at this volume.
/// </summary>
public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public bool IsLive => _options.IsConfigured;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mime = new MimeMessage();

        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));

        if (!string.IsNullOrWhiteSpace(_options.ReplyToEmail))
        {
            mime.ReplyTo.Add(MailboxAddress.Parse(_options.ReplyToEmail));
        }

        // A staging safety net: with a redirect configured, nothing can reach a real
        // family from a test database however wrong the data is.
        if (!string.IsNullOrWhiteSpace(_options.RedirectAllTo))
        {
            mime.To.Add(MailboxAddress.Parse(_options.RedirectAllTo));
            mime.Headers.Add("X-GoSit-Original-To", message.ToEmail);
        }
        else
        {
            mime.To.Add(new MailboxAddress(message.ToName, message.ToEmail));
        }

        mime.Subject = message.Subject;

        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody,
        }.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            // StartTls, matching port 587. SslOnConnect is for 465 and would hang here.
            await client.ConnectAsync(
                _options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls, cancellationToken);

            await client.AuthenticateAsync(
                _options.SmtpUsername, _options.SmtpPassword, cancellationToken);

            await client.SendAsync(mime, cancellationToken);
        }
        catch (AuthenticationException ex)
        {
            // Named separately because the cause is almost always one of two things, and
            // saying so saves an hour: a wrong key, or — on this account — an IP that is
            // not on the SMTP key's allowlist.
            throw new InvalidOperationException(
                "The SMTP relay refused the credentials. Check the SMTP key, and check "
                + "that this machine's outbound IP address is on the provider's allowlist.",
                ex);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None);
            }
        }

        // The recipient address is personal data and does not go in a log line. The
        // subject is a template title, not content, so it is safe and useful.
        logger.LogInformation("Sent an email: {Subject}", message.Subject);
    }
}

/// <summary>
/// Stands in when no SMTP credentials are configured — local development and the tests.
///
/// Records that an email would have been sent, without the recipient, and reports
/// <see cref="IsLive"/> false so the dispatcher marks notifications suppressed rather
/// than sent. Claiming delivery that did not happen would make the audit trail lie about
/// whether someone was told their booking was cancelled.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public bool IsLive => false;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Email not sent, no provider configured: {Subject}", message.Subject);

        return Task.CompletedTask;
    }
}
