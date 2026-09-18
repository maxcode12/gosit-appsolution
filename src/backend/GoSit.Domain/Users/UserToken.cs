using GoSit.Domain.Common;

namespace GoSit.Domain.Users;

public enum UserTokenPurpose
{
    /// <summary>Proves the person signing up controls the mailbox.</summary>
    EmailVerification = 1,

    /// <summary>Authorises setting a new password without knowing the old one.</summary>
    PasswordReset = 2,
}

/// <summary>
/// A single-use, expiring token emailed to someone.
///
/// <b>Only the hash is stored.</b> A password-reset token is a bearer credential — anyone
/// holding it can take over an account — so a database leak must not hand an attacker a
/// working set. The plaintext exists for exactly as long as it takes to build the email,
/// and is never logged, never returned by an API, and never persisted.
///
/// Same reasoning as <see cref="RefreshToken"/>, and deliberately the same shape.
///
/// Single use is enforced by <see cref="Consume"/>: a token that has been redeemed is
/// dead even if it has not expired. Without that, a reset link sitting in a mailbox —
/// or in a forwarded email, or a shared family inbox — stays live for its whole lifetime.
/// </summary>
public sealed class UserToken : Entity
{
    private UserToken()
    {
    }

    public Guid UserId { get; private set; }

    public UserTokenPurpose Purpose { get; private set; }

    /// <summary>SHA-256 of the token. The token itself is never stored.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? ConsumedAtUtc { get; private set; }

    public bool IsConsumed => ConsumedAtUtc is not null;

    /// <summary>
    /// Usable right now: not spent, and not expired.
    ///
    /// Both conditions checked here rather than at the call site, so a caller cannot
    /// accidentally honour a token on one of the two counts.
    /// </summary>
    public bool IsUsableAt(DateTime utcNow) => !IsConsumed && utcNow < ExpiresAtUtc;

    public static UserToken Issue(
        Guid userId,
        UserTokenPurpose purpose,
        string tokenHash,
        DateTime utcNow,
        TimeSpan lifetime)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("An account is required.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("A token hash is required.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException("A token lifetime must be positive.");
        }

        return new UserToken
        {
            UserId = userId,
            Purpose = purpose,
            TokenHash = tokenHash,
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow.Add(lifetime),
        };
    }

    /// <summary>
    /// Spends the token. Idempotent, so a double-submitted form cannot un-spend it, and
    /// the first consumption time is what is kept.
    /// </summary>
    public void Consume(DateTime utcNow) => ConsumedAtUtc ??= utcNow;
}
