using GoSit.Mobile.UI.Abstractions;

namespace GoSit.Mobile.UI.Services;

/// <summary>What the API returns after a successful sign-in or refresh.</summary>
public sealed record AuthTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    Guid UserId,
    string Role);

/// <summary>
/// The signed-in session, held in platform secure storage.
///
/// Keychain on iOS, the Android Keystore behind EncryptedSharedPreferences — not
/// preferences, not a file, and never a log line. A phone is lost or handed to a child
/// far more often than a laptop is, and a refresh token is a fortnight of access to a
/// family's address and a caregiver's earnings.
///
/// Tokens are cached in memory after the first read because secure storage is slow
/// enough to be visible on a screen that renders a list, and every request needs one.
/// </summary>
public sealed class SessionStore(ISecureTokenStore storage)
{
    private const string AccessTokenKey = "gosit.access";
    private const string RefreshTokenKey = "gosit.refresh";
    private const string RoleKey = "gosit.role";

    private string? _accessToken;
    private string? _refreshToken;
    private string? _role;
    private bool _loaded;

    /// <summary>Raised when the session appears or disappears, so the shell can redraw.</summary>
    public event Action? Changed;

    public async Task<bool> IsSignedInAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        // The refresh token, not the access token: an expired access token is a normal
        // state that the client recovers from without troubling anyone.
        return !string.IsNullOrEmpty(_refreshToken);
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _accessToken;
    }

    public async Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _refreshToken;
    }

    /// <summary>Which side of the marketplace this account is on. Routing only.</summary>
    public async Task<string?> GetRoleAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _role;
    }

    public async Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default)
    {
        _accessToken = tokens.AccessToken;
        _refreshToken = tokens.RefreshToken;
        _role = tokens.Role;
        _loaded = true;

        await storage.SetAsync(AccessTokenKey, tokens.AccessToken, cancellationToken);
        await storage.SetAsync(RefreshTokenKey, tokens.RefreshToken, cancellationToken);
        await storage.SetAsync(RoleKey, tokens.Role, cancellationToken);

        Changed?.Invoke();
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _accessToken = null;
        _refreshToken = null;
        _role = null;
        _loaded = true;

        await storage.RemoveAsync(AccessTokenKey, cancellationToken);
        await storage.RemoveAsync(RefreshTokenKey, cancellationToken);
        await storage.RemoveAsync(RoleKey, cancellationToken);

        Changed?.Invoke();
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        _accessToken = await storage.GetAsync(AccessTokenKey, cancellationToken);
        _refreshToken = await storage.GetAsync(RefreshTokenKey, cancellationToken);
        _role = await storage.GetAsync(RoleKey, cancellationToken);
        _loaded = true;
    }
}
