namespace GoSit.Mobile.UI.Abstractions;

public readonly record struct DeviceCoordinates(double Latitude, double Longitude);

/// <summary>
/// Device geolocation used for caregiver proximity search and booking check-in/out.
/// Coordinates are sent to the API only for the specific action that requires them.
/// </summary>
public interface IDeviceLocationService
{
    Task<DeviceCoordinates?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
