using GoSit.Domain.Common;
using GoSit.Domain.Users;
using GoSit.Domain.Users.Events;
using Xunit;

namespace GoSit.UnitTests.Domain;

public class UserTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    private static User CreateUser(UserRole role = UserRole.CareRequester) =>
        User.Register("Requester@Example.com", "hash", "Ada", "Lovelace", role, Now);

    [Fact]
    public void Register_normalizes_email_and_trims_names()
    {
        var user = User.Register("  Requester@Example.com  ", "hash", " Ada ", " Lovelace ", UserRole.CareRequester, Now);

        Assert.Equal("Requester@Example.com", user.Email);
        Assert.Equal("REQUESTER@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal("Ada", user.FirstName);
        Assert.Equal("Lovelace", user.LastName);
    }

    [Fact]
    public void Register_starts_active_and_unverified()
    {
        var user = CreateUser();

        Assert.Equal(UserStatus.Active, user.Status);
        Assert.False(user.EmailVerified);
        Assert.True(user.CanAuthenticate);
    }

    [Fact]
    public void Register_raises_UserRegistered_event()
    {
        var user = CreateUser(UserRole.Caregiver);

        var @event = Assert.Single(user.DomainEvents);
        var registered = Assert.IsType<UserRegisteredDomainEvent>(@event);
        Assert.Equal(user.Id, registered.UserId);
        Assert.Equal(UserRole.Caregiver, registered.Role);
    }

    [Fact]
    public void Register_rejects_admin_role()
    {
        var act = () => User.Register("a@b.com", "hash", "Ada", "Lovelace", UserRole.Admin, Now);

        Assert.Throws<DomainException>(act);
    }

    [Theory]
    [InlineData("", "hash", "Ada", "Lovelace")]
    [InlineData("a@b.com", "", "Ada", "Lovelace")]
    [InlineData("a@b.com", "hash", "", "Lovelace")]
    [InlineData("a@b.com", "hash", "Ada", "")]
    public void Register_requires_all_fields(string email, string hash, string first, string last)
    {
        var act = () => User.Register(email, hash, first, last, UserRole.CareRequester, Now);

        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Suspended_user_cannot_authenticate_and_loses_active_tokens()
    {
        var user = CreateUser();
        user.IssueRefreshToken("hash-1", Now, Lifetime);

        user.Suspend(Now);

        Assert.False(user.CanAuthenticate);
        Assert.All(user.RefreshTokens, token => Assert.True(token.IsRevoked));
    }

    [Fact]
    public void Suspended_user_cannot_be_issued_tokens()
    {
        var user = CreateUser();
        user.Suspend(Now);

        Assert.Throws<DomainException>(() => user.IssueRefreshToken("hash-1", Now, Lifetime));
    }

    [Fact]
    public void ChangePassword_revokes_every_session()
    {
        var user = CreateUser();
        user.IssueRefreshToken("hash-1", Now, Lifetime);
        user.IssueRefreshToken("hash-2", Now, Lifetime);

        user.ChangePassword("new-hash", Now);

        Assert.Equal("new-hash", user.PasswordHash);
        Assert.All(user.RefreshTokens, token => Assert.True(token.IsRevoked));
    }

    [Fact]
    public void UpgradePasswordHash_keeps_sessions_alive()
    {
        var user = CreateUser();
        user.IssueRefreshToken("hash-1", Now, Lifetime);

        user.UpgradePasswordHash("stronger-hash");

        Assert.Equal("stronger-hash", user.PasswordHash);
        Assert.All(user.RefreshTokens, token => Assert.False(token.IsRevoked));
    }

    [Fact]
    public void Rotate_revokes_the_old_token_and_links_the_replacement()
    {
        var user = CreateUser();
        user.IssueRefreshToken("hash-1", Now, Lifetime);

        var replacement = user.RotateRefreshToken("hash-1", "hash-2", Now, Lifetime);

        var original = user.RefreshTokens.Single(t => t.TokenHash == "hash-1");
        Assert.True(original.IsRevoked);
        Assert.Equal("hash-2", original.ReplacedByTokenHash);
        Assert.True(replacement.IsActiveAt(Now));
    }

    [Fact]
    public void Rotate_rejects_an_unknown_token()
    {
        var user = CreateUser();

        Assert.Throws<DomainException>(() =>
            user.RotateRefreshToken("never-issued", "hash-2", Now, Lifetime));
    }

    [Fact]
    public void Replaying_a_rotated_token_revokes_every_session()
    {
        var user = CreateUser();
        user.IssueRefreshToken("hash-1", Now, Lifetime);
        user.RotateRefreshToken("hash-1", "hash-2", Now, Lifetime);

        // "hash-1" was already exchanged; presenting it again signals a stolen token.
        Assert.Throws<DomainException>(() =>
            user.RotateRefreshToken("hash-1", "hash-3", Now, Lifetime));

        Assert.All(user.RefreshTokens, token => Assert.True(token.IsRevoked));
    }

    [Fact]
    public void Rotate_rejects_an_expired_token()
    {
        var user = CreateUser();
        user.IssueRefreshToken("hash-1", Now, TimeSpan.FromDays(1));

        var afterExpiry = Now.AddDays(2);

        Assert.Throws<DomainException>(() =>
            user.RotateRefreshToken("hash-1", "hash-2", afterExpiry, Lifetime));
    }

    [Fact]
    public void Revoking_an_unknown_token_is_a_no_op()
    {
        var user = CreateUser();
        user.IssueRefreshToken("hash-1", Now, Lifetime);

        user.RevokeRefreshToken("never-issued", Now);

        Assert.True(user.RefreshTokens.Single().IsActiveAt(Now));
    }

    [Fact]
    public void Expired_token_is_not_active()
    {
        var user = CreateUser();
        var token = user.IssueRefreshToken("hash-1", Now, TimeSpan.FromMinutes(30));

        Assert.True(token.IsActiveAt(Now.AddMinutes(29)));
        Assert.False(token.IsActiveAt(Now.AddMinutes(31)));
    }

    [Fact]
    public void RecordSuccessfulLogin_stamps_the_time()
    {
        var user = CreateUser();

        user.RecordSuccessfulLogin(Now);

        Assert.Equal(Now, user.LastLoginAtUtc);
    }
}
