namespace GoSit.Mobile.UI.Abstractions;

/// <summary>
/// Network reachability, used for offline resilience in the mobile client.
/// </summary>
public interface IConnectivityService
{
    bool IsConnected { get; }
}
