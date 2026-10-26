using GoSit.Domain.Common;
using GoSit.Domain.Enquiries;
using Xunit;

namespace GoSit.UnitTests.Domain;

/// <summary>
/// A message from a member of the public who has no account. The rules that matter are the
/// ones protecting the sender (nothing is kept without consent) and the team (the queue only
/// ever holds something readable that someone can act on).
/// </summary>
public class EnquiryTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 15, 0, 0, DateTimeKind.Utc);

    private static Enquiry Valid(
        string name = "Ada Lovelace",
        string email = "ada@example.com",
        EnquiryKind kind = EnquiryKind.Family,
        string message = "I would like to know when you open in Mississauga.",
        bool consent = true) =>
        Enquiry.Submit(name, email, kind, message, "website", consent, Now);

    [Fact]
    public void A_submitted_enquiry_is_new_and_records_when_consent_was_given()
    {
        var enquiry = Valid();

        Assert.Equal(EnquiryStatus.New, enquiry.Status);
        Assert.Equal(Now, enquiry.CreatedAtUtc);
        Assert.Equal(Now, enquiry.ConsentedAtUtc);
        Assert.Equal("website", enquiry.Source);
        Assert.Null(enquiry.HandledAtUtc);
    }

    [Fact]
    public void Text_is_trimmed_before_it_is_kept()
    {
        var enquiry = Valid(name: "  Ada  ", email: " ada@example.com ", message: "   A message long enough to count.   ");

        Assert.Equal("Ada", enquiry.Name);
        Assert.Equal("ada@example.com", enquiry.Email);
        Assert.Equal("A message long enough to count.", enquiry.Message);
    }

    [Fact]
    public void Nothing_is_kept_without_consent()
    {
        var ex = Assert.Throws<DomainException>(() => Valid(consent: false));

        Assert.Contains("Consent", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_name_is_required(string name) =>
        Assert.Throws<DomainException>(() => Valid(name: name));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_email_address_is_required(string email) =>
        Assert.Throws<DomainException>(() => Valid(email: email));

    [Fact]
    public void A_message_must_be_long_enough_to_mean_something()
    {
        var ex = Assert.Throws<DomainException>(() => Valid(message: "hi"));

        Assert.Contains("at least", ex.Message);
    }

    [Fact]
    public void A_message_has_a_ceiling()
    {
        Assert.Throws<DomainException>(
            () => Valid(message: new string('x', Enquiry.MaxMessageLength + 1)));

        // The limit itself is fine.
        Valid(message: new string('x', Enquiry.MaxMessageLength));
    }

    [Fact]
    public void A_name_has_a_ceiling() =>
        Assert.Throws<DomainException>(() => Valid(name: new string('n', Enquiry.MaxNameLength + 1)));

    [Fact]
    public void A_kind_that_does_not_exist_is_refused() =>
        Assert.Throws<DomainException>(() => Valid(kind: (EnquiryKind)0));

    [Fact]
    public void Staff_can_mark_an_enquiry_handled_once()
    {
        var staff = Guid.NewGuid();
        var enquiry = Valid();

        enquiry.MarkHandled(staff, Now.AddHours(2));

        Assert.Equal(EnquiryStatus.Handled, enquiry.Status);
        Assert.Equal(staff, enquiry.HandledByUserId);
        Assert.Equal(Now.AddHours(2), enquiry.HandledAtUtc);

        Assert.Throws<DomainException>(() => enquiry.MarkHandled(staff, Now.AddHours(3)));
        Assert.Throws<DomainException>(() => enquiry.MarkSpam(staff, Now.AddHours(3)));
    }

    [Fact]
    public void Spam_is_kept_and_marked_not_deleted()
    {
        var enquiry = Valid();

        enquiry.MarkSpam(Guid.NewGuid(), Now.AddMinutes(5));

        Assert.Equal(EnquiryStatus.Spam, enquiry.Status);
        Assert.NotNull(enquiry.HandledAtUtc);
    }

    [Fact]
    public void Closing_an_enquiry_needs_a_staff_member() =>
        Assert.Throws<DomainException>(() => Valid().MarkHandled(Guid.Empty, Now));
}
