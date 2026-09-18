using GoSit.Domain.Users;
using Xunit;

namespace GoSit.UnitTests.Domain;

/// <summary>
/// Per-account lockout.
///
/// The API rate-limits credential endpoints per IP, which stops one machine hammering the
/// login and does nothing about the attack that matters: a distributed guess at one known
/// address, a few tries from each of a thousand IPs. What sits behind a GoSit account —
/// a family's home address, a child's routine, a disabled adult's care plan — makes that
/// worth stopping.
///
/// The tests that earn their place are the ones about not overcorrecting. A lockout is a
/// weapon pointed at the account it protects, and the two things that keep it from being
/// used that way are that it always expires and that a successful sign-in clears it.
/// </summary>
public class UserLockoutTests
{
    private static readonly DateTime Now = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

    private static User CreateUser() =>
        User.Register("ada@example.com", "hash", "Ada", "Lovelace", UserRole.CareRequester, Now);

    private static User AfterFailures(int count, DateTime? at = null)
    {
        var user = CreateUser();

        for (var i = 0; i < count; i++)
        {
            user.RecordFailedLogin(at ?? Now);
        }

        return user;
    }

    [Fact]
    public void A_new_account_is_not_locked_out()
    {
        Assert.False(CreateUser().IsLockedOutAt(Now));
    }

    /// <summary>
    /// Ten wrong passwords is far more often an old password in someone's manager than an
    /// attack, so the tolerance has to be wide enough to absorb that.
    /// </summary>
    [Fact]
    public void A_few_wrong_passwords_do_not_lock_the_account()
    {
        var user = AfterFailures(User.MaximumFailedAttempts - 1);

        Assert.False(user.IsLockedOutAt(Now));
    }

    [Fact]
    public void The_account_locks_once_the_tolerance_is_spent()
    {
        var user = AfterFailures(User.MaximumFailedAttempts);

        Assert.True(user.IsLockedOutAt(Now));
    }

    /// <summary>
    /// The single most important property here. A lockout that did not expire would hand
    /// an attacker a way to keep a caregiver out of the app before a shift, or a family
    /// out on the evening they need to reach one — by doing nothing but typing badly.
    /// </summary>
    [Fact]
    public void A_lockout_always_expires()
    {
        var user = AfterFailures(User.MaximumFailedAttempts);

        Assert.False(user.IsLockedOutAt(Now.AddHours(2)));
    }

    /// <summary>
    /// Waiting out a lockout and resuming costs more each time, which is what turns
    /// thousands of guesses an hour into a handful.
    /// </summary>
    [Fact]
    public void Each_successive_lockout_lasts_longer()
    {
        var user = AfterFailures(User.MaximumFailedAttempts);
        var first = user.LockedOutUntilUtc!.Value;

        // Waited it out, then spent the tolerance again.
        var later = first.AddSeconds(1);

        for (var i = 0; i < User.MaximumFailedAttempts; i++)
        {
            user.RecordFailedLogin(later);
        }

        var second = user.LockedOutUntilUtc!.Value;

        Assert.True(
            second - later > first - Now,
            "A second lockout should last longer than the first.");
    }

    /// <summary>The escalation is capped, so a lockout never becomes effectively permanent.</summary>
    [Fact]
    public void The_lockout_window_is_capped()
    {
        var user = AfterFailures(500);

        Assert.False(user.IsLockedOutAt(Now.AddHours(2)));
    }

    /// <summary>
    /// Getting in proves the guessing was not guessing. Without this, a person who
    /// mistyped nine times and then signed in would carry those failures forever and lock
    /// on their next single slip.
    /// </summary>
    [Fact]
    public void A_successful_sign_in_clears_the_count()
    {
        var user = AfterFailures(User.MaximumFailedAttempts - 1);

        user.RecordSuccessfulLogin(Now);

        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedOutUntilUtc);
    }

    [Fact]
    public void Clearing_the_lockout_lets_the_account_back_in_immediately()
    {
        var user = AfterFailures(User.MaximumFailedAttempts);

        user.ClearLockout();

        Assert.False(user.IsLockedOutAt(Now));
        Assert.Equal(0, user.FailedLoginAttempts);
    }
}
