using GoSit.Domain.Users;

namespace GoSit.Application.Abstractions;

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

/// <summary>
/// Issues JWT access tokens and the opaque refresh tokens that accompany them.
/// </summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    /// <summary>
    /// Generates a cryptographically random refresh token. The caller stores only
    /// <see cref="RefreshTokenPair.Hash"/> and returns <see cref="RefreshTokenPair.Value"/> to the client.
    /// </summary>
    RefreshTokenPair CreateRefreshToken();

    /// <summary>Hashes a client-presented refresh token so it can be matched against storage.</summary>
    string HashRefreshToken(string refreshToken);

    TimeSpan RefreshTokenLifetime { get; }
}

public sealed record RefreshTokenPair(string Value, string Hash);
