using System.Net;
using System.Net.Http.Json;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Location on check-in and check-out, through the API.
///
/// The arithmetic is unit tested. What these hold is the rule that makes the feature
/// safe to ship: **it never refuses**. A caregiver whose phone cannot fix a location, or
/// fixes a bad one, still starts their shift — the booking records what was known. The
/// opposite design leaves someone standing at a family's door unable to begin work, and
/// falls hardest on whoever has the worst handset.
/// </summary>
[Collection(nameof(ApiCollection))]
public class ArrivalVerificationTests(GoSitApiFactory factory)
{
    private static readonly DateTime Start = NextWeekdayAtUtc(DayOfWeek.Wednesday, 15);
    private static readonly DateTime End = Start.AddHours(4);

    // Toronto City Hall, which is what CreateCareRequesterProfileAsync stores.
    private const double HomeLat = 43.6534;
    private const double HomeLon = -79.3841;

    /// <summary>An accepted booking, ready to be checked in to.</summary>
    private async Task<(Guid BookingId, HttpClient Caregiver, HttpClient Requester)> ReadyAsync(
        bool geocodeHome = true)
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync(hourlyRate: 30m);

        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();

        if (geocodeHome)
        {
            await factory.SetRequesterCoordinatesAsync(requester, HomeLat, HomeLon);
        }

        var recipientId = await requester.AddCareRecipientAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
            .EnsureSuccessStatusCode();

        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - Start);

        return (booking.Id, caregiver, requester);
    }

    private static Task<HttpResponseMessage> CheckInAsync(
        HttpClient caregiver,
        Guid bookingId,
        object? fix = null) =>
        caregiver.PostAsJsonAsync($"/api/v1/bookings/{bookingId}/check-in", fix ?? new { });

    // ---- it records ----

    [Fact]
    public async Task Checking_in_at_the_address_is_recorded_as_such()
    {
        var (bookingId, caregiver, _) = await ReadyAsync();

        var response = await CheckInAsync(
            caregiver, bookingId, new { latitude = HomeLat, longitude = HomeLon });

        response.EnsureSuccessStatusCode();

        var booking = (await response.Content.ReadFromJsonAsync<BookingBody>())!;

        Assert.Equal("InProgress", booking.Status);
        Assert.Equal("AtTheAddress", booking.CheckInVerification);
    }

    [Fact]
    public async Task Checking_in_from_far_away_is_recorded_and_still_allowed()
    {
        var (bookingId, caregiver, _) = await ReadyAsync();

        // Roughly 9 km north — nobody's GPS error.
        var response = await CheckInAsync(
            caregiver, bookingId, new { latitude = HomeLat + 0.081, longitude = HomeLon });

        response.EnsureSuccessStatusCode();

        var booking = (await response.Content.ReadFromJsonAsync<BookingBody>())!;

        // Recorded, not refused. Whether it matters is for the family to judge.
        Assert.Equal("InProgress", booking.Status);
        Assert.Equal("AwayFromTheAddress", booking.CheckInVerification);
    }

    /// <summary>
    /// The family is the audience for this. A caregiver's own record of where they were
    /// is worth little if the person who booked them cannot see it.
    /// </summary>
    [Fact]
    public async Task The_family_can_see_where_the_caregiver_checked_in()
    {
        var (bookingId, caregiver, requester) = await ReadyAsync();

        await CheckInAsync(caregiver, bookingId, new { latitude = HomeLat, longitude = HomeLon });

        var booking = await requester.GetFromJsonAsync<BookingBody>(
            $"/api/v1/bookings/{bookingId}");

        Assert.Equal("AtTheAddress", booking!.CheckInVerification);
    }

    [Fact]
    public async Task Checking_out_records_its_own_location()
    {
        var (bookingId, caregiver, _) = await ReadyAsync();

        await CheckInAsync(caregiver, bookingId, new { latitude = HomeLat, longitude = HomeLon });

        var response = await caregiver.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/check-out",
            new { latitude = HomeLat, longitude = HomeLon });

        response.EnsureSuccessStatusCode();

        var booking = (await response.Content.ReadFromJsonAsync<BookingBody>())!;

        Assert.Equal("Completed", booking.Status);
        Assert.Equal("AtTheAddress", booking.CheckOutVerification);
    }

    // ---- it never refuses ----

    [Fact]
    public async Task A_check_in_with_no_location_still_starts_the_shift()
    {
        var (bookingId, caregiver, _) = await ReadyAsync();

        var response = await CheckInAsync(caregiver, bookingId);

        response.EnsureSuccessStatusCode();

        var booking = (await response.Content.ReadFromJsonAsync<BookingBody>())!;

        Assert.Equal("InProgress", booking.Status);
        Assert.Equal("NotProvided", booking.CheckInVerification);
    }

    /// <summary>
    /// No body at all — the shape an older client, or a request that never carried one,
    /// would send. It must behave exactly like a declined permission.
    /// </summary>
    [Fact]
    public async Task A_check_in_with_no_body_at_all_still_starts_the_shift()
    {
        var (bookingId, caregiver, _) = await ReadyAsync();

        var response = await caregiver.PostAsync($"/api/v1/bookings/{bookingId}/check-in", null);

        response.EnsureSuccessStatusCode();

        var booking = (await response.Content.ReadFromJsonAsync<BookingBody>())!;

        Assert.Equal("NotProvided", booking.CheckInVerification);
    }

    /// <summary>
    /// A nonsense fix is the device's problem and must not become the caregiver's. It is
    /// dropped rather than rejected, which is indistinguishable from having none.
    /// </summary>
    [Theory]
    [InlineData(91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    [InlineData(-91.0, -181.0)]
    public async Task An_impossible_fix_is_ignored_rather_than_refused(double lat, double lon)
    {
        var (bookingId, caregiver, _) = await ReadyAsync();

        var response = await CheckInAsync(
            caregiver, bookingId, new { latitude = lat, longitude = lon });

        response.EnsureSuccessStatusCode();

        var booking = (await response.Content.ReadFromJsonAsync<BookingBody>())!;

        Assert.Equal("InProgress", booking.Status);
        Assert.Equal("NotProvided", booking.CheckInVerification);
    }

    /// <summary>
    /// Until geocoding is configured most addresses have no coordinates. There is nothing
    /// to compare against, and saying so is different from saying the caregiver was
    /// somewhere else.
    /// </summary>
    [Fact]
    public async Task An_ungeocoded_home_reports_not_comparable_not_away()
    {
        var (bookingId, caregiver, _) = await ReadyAsync(geocodeHome: false);

        var response = await CheckInAsync(
            caregiver, bookingId, new { latitude = HomeLat, longitude = HomeLon });

        response.EnsureSuccessStatusCode();

        var booking = (await response.Content.ReadFromJsonAsync<BookingBody>())!;

        Assert.Equal("NotComparable", booking.CheckInVerification);
    }

    // ---- who may send one ----

    /// <summary>
    /// A family cannot check a caregiver in, with or without a location — the existing
    /// rule, restated because the new body is a new way to try.
    /// </summary>
    [Fact]
    public async Task A_family_cannot_check_in_on_the_caregivers_behalf()
    {
        var (bookingId, _, requester) = await ReadyAsync();

        var response = await CheckInAsync(
            requester, bookingId, new { latitude = HomeLat, longitude = HomeLon });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
