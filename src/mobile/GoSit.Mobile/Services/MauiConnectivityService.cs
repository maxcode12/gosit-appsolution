using GoSit.Mobile.UI.Abstractions;

namespace GoSit.Mobile.Services;

/// <inheritdoc />
public sealed class MauiConnectivityService : IConnectivityService
{
    public bool IsConnected =>
        Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
}
