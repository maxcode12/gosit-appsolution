using GoSit.Application.Abstractions;
using GoSit.Domain.Enquiries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoSit.Infrastructure.Email;

public sealed class EnquiryOptions
{
    public const string SectionName = "Enquiries";

    /// <summary>
    /// Where to send "a message has arrived". Blank means no alert: the enquiry is still
    /// saved and shows in the staff queue, nothing is mailed.
    /// </summary>
    public string NotifyEmail { get; set; } = string.Empty;
}

/// <summary>
/// Emails the team that a message is waiting.
///
/// The email carries no part of the message, and the subject names only the kind. The
/// sender's details are personal information and stay in the database and the staff
/// queue; email is the wrong place to scatter them.
/// </summary>
public sealed class EmailEnquiryAlert(
    IEmailSender sender,
    IOptions<EnquiryOptions> options,
    ILogger<EmailEnquiryAlert> logger) : IEnquiryAlert
{
    public async Task NotifyAsync(Guid enquiryId, EnquiryKind kind, CancellationToken cancellationToken)
    {
        var to = options.Value.NotifyEmail?.Trim();

        if (string.IsNullOrEmpty(to))
        {
            return;
        }

        try
        {
            await sender.SendAsync(
                new EmailMessage(
                    ToEmail: to,
                    ToName: "GoSit team",
                    Subject: $"New GoSit enquiry ({kind})",
                    HtmlBody: "<p>A new message has arrived through the website contact form.</p>"
                        + "<p>Open the admin area to read it and reply.</p>",
                    TextBody: "A new message has arrived through the website contact form.\n\n"
                        + "Open the admin area to read it and reply."),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // The enquiry is already saved. Failing the visitor's request over a mail problem
            // would lose nothing but make a successful submission look like a failure.
            logger.LogWarning(ex, "Could not send the alert for enquiry {EnquiryId}", enquiryId);
        }
    }
}
