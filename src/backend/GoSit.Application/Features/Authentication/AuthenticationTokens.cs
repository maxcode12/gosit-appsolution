namespace GoSit.Application.Features.Authentication;

/// <summary>
/// Token pair returned by register, login, and refresh.
/// The refresh token is opaque to clients; only its hash is stored server-side.
/// </summary>
public sealed record AuthenticationTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    Guid UserId,
    string Role);
