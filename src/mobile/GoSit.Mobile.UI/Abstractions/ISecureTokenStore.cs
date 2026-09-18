namespace GoSit.Mobile.UI.Abstractions;

/// <summary>
/// Platform-backed storage for authentication tokens. Implemented over
/// MAUI SecureStorage on device; substituted with a fake in tests.
/// Tokens must never be written to plain preferences or logs.
/// </summary>
public interface ISecureTokenStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
