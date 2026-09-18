using GoSit.Domain.Common;

namespace GoSit.Domain.Users;

/// <summary>
/// A single issued refresh token. Only the SHA-256 hash of the token value is
/// persisted, so a database disclosure does not yield usable tokens.
/// Rotation replaces one token with the next and records the link, which lets
/// reuse of an already-rotated token be detected.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    /// <summary>Hash of the token that replaced this one during rotation.</summary>
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsExpiredAt(DateTime utcNow) => utcNow >= ExpiresAtUtc;

    public bool IsActiveAt(DateTime utcNow) => !IsRevoked && !IsExpiredAt(utcNow);

    internal static RefreshToken Issue(Guid userId, string tokenHash, DateTime utcNow, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("A refresh token hash is required.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException("Refresh token lifetime must be positive.");
        }

        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow.Add(lifetime),
        };
    }

    internal void Revoke(DateTime utcNow, string? replacedByTokenHash = null)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAtUtc = utcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
