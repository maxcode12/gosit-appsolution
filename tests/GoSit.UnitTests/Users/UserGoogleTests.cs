using GoSit.Domain.Common;
using GoSit.Domain.Users;

namespace GoSit.UnitTests.Users;

public sealed class UserGoogleTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    private static User NewGoogleUser(UserRole role = UserRole.Caregiver) =>
        User.RegisterWithGoogle("sub-1", "Sam@Example.com", "Sam", "Lee", role, Now);

    [Fact]
    public void A_google_account_has_no_password_and_a_verified_email()
    {
        var user = NewGoogleUser();

        Assert.False(user.HasPassword);
        Assert.True(user.EmailVerified);
        Assert.Equal("sub-1", user.GoogleSubject);
        Assert.Equal("SAM@EXAMPLE.COM", user.NormalizedEmail);
    }

    [Fact]
    public void A_missing_family_name_gets_a_placeholder_rather_than_failing()
    {
        var user = User.RegisterWithGoogle("sub-1", "sam@example.com", "Sam", "", UserRole.CareRequester, Now);

        Assert.Equal("-", user.LastName);
    }

    [Fact]
    public void Google_sign_up_cannot_create_staff()
    {
        Assert.Throws<DomainException>(() => NewGoogleUser(UserRole.Admin));
    }

    [Fact]
    public void Linking_marks_the_email_verified_and_is_idempotent()
    {
        var user = User.Register("sam@example.com", "hash", "Sam", "Lee", UserRole.CareRequester, Now);
        Assert.False(user.EmailVerified);

        user.LinkGoogle("sub-1");
        user.LinkGoogle("sub-1");

        Assert.True(user.EmailVerified);
        Assert.Equal("sub-1", user.GoogleSubject);
        Assert.True(user.HasPassword);
    }

    [Fact]
    public void An_account_cannot_be_repointed_at_a_different_google_account()
    {
        var user = NewGoogleUser();

        Assert.Throws<DomainException>(() => user.LinkGoogle("sub-2"));
        Assert.Equal("sub-1", user.GoogleSubject);
    }
}
