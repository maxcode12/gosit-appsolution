using GoSit.Mobile.UI.Abstractions;

namespace GoSit.Mobile.Services;

/// <inheritdoc />
public sealed class MauiSecureTokenStore : ISecureTokenStore
{
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        SecureStorage.Default.GetAsync(key);

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) =>
        SecureStorage.Default.SetAsync(key, value);

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }
}
