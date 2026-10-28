using System.ComponentModel.DataAnnotations;

namespace GoSit.Infrastructure.Email;

/// <summary>
/// SMTP configuration.
///
/// GoSit sends through Brevo's relay. The username and key come from Key Vault in every
/// deployed environment and from user secrets locally — never from appsettings.json,
/// which is checked in.
///
/// <b>Operational note.</b> Brevo can restrict an SMTP key to an allowlist of IP
/// addresses, and GoSit's account has that switched on. Anywhere the API runs has to be
/// on that list or every send fails: a developer's home connection, and every outbound
/// address of the Azure plan. App Service outbound addresses change when the plan scales
/// or is moved, so this is worth checking when sends start failing for no other reason.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = "smtp-relay.brevo.com";

    /// <summary>
    /// 587 with STARTTLS. Not 465 — that is implicit TLS, which MailKit handles under a
    /// different SecureSocketOptions, and mixing the two is the usual cause of a hang
    /// rather than an error.
    /// </summary>
    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    /// <summary>The Brevo SMTP login, e.g. <c>b6494f001@smtp-brevo.com</c>.</summary>
    public string SmtpUsername { get; set; } = string.Empty;

    /// <summary>The SMTP key. Secret — never logged, never in source.</summary>
    public string SmtpPassword { get; set; } = string.Empty;

    /// <summary>
    /// Who mail comes from. Must be a sender Brevo has verified for the account, or it
    /// is rejected at send time rather than silently dropped.
    /// </summary>
    [EmailAddress]
    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "GoSit";

    /// <summary>
    /// Where replies go. Left empty, replies come back to <see cref="FromEmail"/>, which
    /// is usually a no-reply nobody reads — so this is worth setting to something a
    /// person actually monitors.
    /// </summary>
    public string ReplyToEmail { get; set; } = string.Empty;

    /// <summary>
    /// Redirects every outgoing email here, whoever it was addressed to.
    ///
    /// For staging and for local work against real credentials: it makes it impossible
    /// to email a real family from a test database, which is the single worst accident
    /// this system can have. The original recipient is preserved in a header so a test
    /// can still tell who it was meant for.
    /// </summary>
    /// <remarks>
    /// Null, not empty, when unset: <c>[EmailAddress]</c> rejects an empty string, and an
    /// unset option must not stop the sender being constructed at all.
    /// </remarks>
    [EmailAddress]
    public string? RedirectAllTo { get; set; }

    /// <summary>True when enough is configured to actually send.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SmtpUsername)
        && !string.IsNullOrWhiteSpace(SmtpPassword)
        && !string.IsNullOrWhiteSpace(FromEmail);
}
