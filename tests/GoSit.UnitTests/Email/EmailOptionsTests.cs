using System.ComponentModel.DataAnnotations;
using GoSit.Infrastructure.Email;

namespace GoSit.UnitTests.Email;

public class EmailOptionsTests
{
    [Fact]
    public void A_configured_sender_with_nothing_else_set_is_valid()
    {
        // The optional settings left unset used to fail validation, which stopped the SMTP
        // sender being constructed and turned every email-sending request into a 500.
        var options = new EmailOptions
        {
            SmtpUsername = "login@smtp-brevo.com",
            SmtpPassword = "key",
            FromEmail = "no-reply@gosits.com",
        };

        var results = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true));
        Assert.True(options.IsConfigured);
    }

    [Fact]
    public void A_malformed_redirect_address_is_still_rejected()
    {
        var options = new EmailOptions { RedirectAllTo = "not-an-address" };

        Assert.False(Validator.TryValidateObject(options, new ValidationContext(options), [], validateAllProperties: true));
    }
}
