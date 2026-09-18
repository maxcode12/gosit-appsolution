using GoSit.Mobile.UI.Abstractions;

namespace GoSit.Mobile.Services;

/// <summary>
/// The device's location, asked for politely and given up on quickly.
///
/// Every path returns null rather than throwing. A location is evidence on a check-in,
/// not a condition of one, and a caregiver standing at a family's door must be able to
/// start work whether or not their phone can say where it is. See the API's
/// <c>ArrivalEvidence</c> for the other half of that decision.
///
/// The permission is requested here rather than at startup. Asked for on first launch it
/// is a question about nothing, and the answer is usually no; asked when someone presses
/// "I have arrived", the reason is on the screen behind the dialog.
/// </summary>
public sealed class MauiDeviceLocationService : IDeviceLocationService
{
    /// <summary>
    /// How long to wait for a fix.
    ///
    /// A phone will happily spend a minute failing to find a satellite indoors. Ten
    /// seconds is long enough for a cold start outdoors and short enough that the button
    /// does not feel broken — and a button that feels broken gets pressed again.
    /// </summary>
    private static readonly TimeSpan FixTimeout = TimeSpan.FromSeconds(10);

    public async Task<DeviceCoordinates?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await HasPermissionAsync())
            {
                return null;
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, FixTimeout);

            // The last known fix first: it is instant, and a position from a few minutes
            // ago is more than accurate enough for "were they at this address".
            var location = await Geolocation.Default.GetLastKnownLocationAsync()
                ?? await Geolocation.Default.GetLocationAsync(request, cancellationToken);

            return location is null
                ? null
                : new DeviceCoordinates(location.Latitude, location.Longitude);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            /*
             * Everything the platform can throw here means the same thing to the caller:
             * no location. Permission refused, location services switched off, the
             * feature unsupported on this device, a timeout — none of them is a reason to
             * stop someone starting a shift, and distinguishing them would only produce
             * messages nobody can act on while standing in a doorway.
             */
            return null;
        }
    }

    /// <summary>
    /// Asks once, on the main thread — the platform requires it, and a permission request
    /// off it fails silently on Android.
    /// </summary>
    private static async Task<bool> HasPermissionAsync()
    {
        var status = await MainThread.InvokeOnMainThreadAsync(
            Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>);

        if (status == PermissionStatus.Granted)
        {
            return true;
        }

        // Refused before, and on Android permanently. Asking again shows nothing and
        // returns the same answer, so the check-in proceeds without a location instead.
        if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
        {
            return false;
        }

        status = await MainThread.InvokeOnMainThreadAsync(
            Permissions.RequestAsync<Permissions.LocationWhenInUse>);

        return status == PermissionStatus.Granted;
    }
}
