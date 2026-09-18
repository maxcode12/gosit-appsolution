using GoSit.Mobile.UI.Abstractions;

namespace GoSit.Mobile.UI.Services;

/// <summary>A booking as the mobile app needs it. A subset of the API's response.</summary>
public sealed record MobileBooking(
    Guid Id,
    string Status,
    string CaregiverFirstName,
    string CareRequesterFirstName,
    string CareServiceName,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    /// <summary>Null until the caregiver accepts — only <see cref="Area"/> is sent before then.</summary>
    MobileAddress? Address,
    MobileArea Area,
    bool HouseholdDetailsDisclosed,
    bool AddressDisclosed,
    string? Instructions,
    decimal HourlyRate,
    string PricingUnit,
    decimal? NightlyRate,
    int? Nights,
    string CareLocation,
    decimal EstimatedTotal,
    decimal? ActualTotal,
    DateTime? CheckedInAtUtc,
    DateTime? CheckedOutAtUtc,
    string CheckInVerification,
    string CheckOutVerification,
    IReadOnlyList<MobileRecipient> Recipients,
    IReadOnlyList<MobilePet> Pets);

public sealed record MobileAddress(
    string Line1, string? Line2, string City, string Province, string PostalCode);

public sealed record MobileRecipient(
    Guid CareRecipientId, string FirstName, int Age, string RecipientType, bool HasRecordedCareNeeds);

/// <summary>City, province and postal prefix: enough to judge a request, not to find the door.</summary>
public sealed record MobileArea(string City, string Province, string PostalCodePrefix);

public sealed record MobilePet(
    Guid PetId, string Name, string Species, string? Breed, string? Size, MobilePetNotes? CareNotes);

/// <summary>Sent only once the caregiver has accepted the booking.</summary>
public sealed record MobilePetNotes(
    string? FeedingInstructions, string? Medication, string? Behaviour, string? VetClinic, string? OtherNotes);

/// <summary>A Stripe payment intent, as much as the Payment Element needs to mount.</summary>
public sealed record MobilePaymentIntent(decimal Amount, string Currency, string ClientSecret);

/// <summary>A booking's receipt. No card details — see PaymentContracts.cs on the API.</summary>
public sealed record MobilePaymentReceipt(
    decimal Amount, decimal RefundedAmount, string Status, DateTime? SucceededAtUtc);

/// <summary>
/// The caregiver's jobs, and the two transitions that need to work on a doorstep.
/// </summary>
public sealed class BookingService(GoSitApiClient api, IDeviceLocationService location)
{
    public async Task<IReadOnlyList<MobileBooking>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await api.GetAsync<List<MobileBooking>>("/api/v1/bookings", cancellationToken) ?? [];

    public Task<MobileBooking?> GetAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        api.GetAsync<MobileBooking>($"/api/v1/bookings/{bookingId}", cancellationToken);

    public Task<MobileBooking?> AcceptAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        api.PostAsync<MobileBooking>($"/api/v1/bookings/{bookingId}/accept", null, cancellationToken);

    public Task<MobileBooking?> DeclineAsync(
        Guid bookingId,
        string? reason,
        CancellationToken cancellationToken = default) =>
        api.PostAsync<MobileBooking>(
            $"/api/v1/bookings/{bookingId}/decline", new { reason }, cancellationToken);

    public Task<MobileBooking?> CheckInAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        WithLocationAsync($"/api/v1/bookings/{bookingId}/check-in", cancellationToken);

    public Task<MobileBooking?> CheckOutAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        WithLocationAsync($"/api/v1/bookings/{bookingId}/check-out", cancellationToken);

    public Task<MobilePaymentIntent?> StartPaymentAsync(
        Guid bookingId, CancellationToken cancellationToken = default) =>
        api.PostAsync<MobilePaymentIntent>(
            $"/api/v1/bookings/{bookingId}/payment-intent", null, cancellationToken);

    public async Task<MobilePaymentReceipt?> GetReceiptAsync(
        Guid bookingId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await api.GetAsync<MobilePaymentReceipt>(
                $"/api/v1/bookings/{bookingId}/payment", cancellationToken);
        }
        catch (ApiException ex) when (ex.Status == System.Net.HttpStatusCode.NotFound)
        {
            // No payment has been started yet — not an error, just nothing to show.
            return null;
        }
    }

    /// <summary>
    /// Sends the device's location if it will give one, and goes without if it will not.
    ///
    /// This is the whole reason the app is native, and also the place where being native
    /// is most tempting to overreach. A location that cannot be had — permission refused,
    /// no fix indoors, an old handset, airplane mode — must not stop a caregiver starting
    /// work at a family's door. The API records "not provided" and the shift begins.
    ///
    /// Any failure from the platform is swallowed for the same reason: a location service
    /// that throws must not become a caregiver who cannot work.
    /// </summary>
    private async Task<MobileBooking?> WithLocationAsync(
        string path,
        CancellationToken cancellationToken)
    {
        DeviceCoordinates? fix = null;

        try
        {
            fix = await location.GetCurrentAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // No location. Proceed without one.
        }

        object body = fix is { } coordinates
            ? new { latitude = coordinates.Latitude, longitude = coordinates.Longitude }
            : new { };

        return await api.PostAsync<MobileBooking>(path, body, cancellationToken);
    }
}
