using System.Net.Http.Json;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Telling people that a booking changed.
///
/// `NotificationType` declared BookingRequested, Accepted, Declined, Cancelled and
/// Completed from Phase 5, and not one of them was ever raised — only messaging created a
/// notification. A caregiver was never told a family had booked them; a family was never
/// told their request was accepted, declined, or cancelled the night before.
///
/// Two rules run through all of these, and both are asserted rather than assumed: the
/// person who acted is not notified about their own action, and a notification carries a
/// summary rather than the reason. They are delivered by email and land on lock screens.
/// </summary>
[Collection(nameof(ApiCollection))]
public class BookingNotificationTests(GoSitApiFactory factory)
{
    private static readonly DateTime Start = NextWeekdayAtUtc(DayOfWeek.Wednesday, 15);

    private sealed record NotificationBody(
        Guid Id, string Type, string Title, string? Body,
        string? RelatedEntityType, Guid? RelatedEntityId, bool IsRead);

    private sealed record NotificationListBody(
        IReadOnlyList<NotificationBody> Notifications, int UnreadCount);

    private static async Task<IReadOnlyList<NotificationBody>> NotificationsAsync(HttpClient client)
    {
        var list = await client.GetFromJsonAsync<NotificationListBody>("/api/v1/notifications");
        return list!.Notifications;
    }

    private async Task<(HttpClient Requester, HttpClient Caregiver, BookingBody Booking)>
        RequestedBookingAsync()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();

        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        var booking = await requester.CreateBookingAsync(
            caregiverId, recipientId, Start, Start.AddHours(4));

        return (requester, await factory.CaregiverClientForAsync(caregiverId), booking);
    }

    [Fact]
    public async Task A_caregiver_is_told_when_a_family_books_them()
    {
        var (_, caregiver, _) = await RequestedBookingAsync();

        var notifications = await NotificationsAsync(caregiver);

        Assert.Contains(notifications, n => n.Type == "BookingRequested");
    }

    /// <summary>
    /// The family did the booking. Telling them about it is noise, and noise is what
    /// trains people to ignore the notification that matters.
    /// </summary>
    [Fact]
    public async Task The_family_is_not_told_about_their_own_booking()
    {
        var (requester, _, _) = await RequestedBookingAsync();

        var notifications = await NotificationsAsync(requester);

        Assert.DoesNotContain(notifications, n => n.Type == "BookingRequested");
    }

    [Fact]
    public async Task A_family_is_told_when_their_booking_is_accepted()
    {
        var (requester, caregiver, booking) = await RequestedBookingAsync();

        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
            .EnsureSuccessStatusCode();

        Assert.Contains(await NotificationsAsync(requester), n => n.Type == "BookingAccepted");
    }

    /// <summary>
    /// The reason a caregiver gives for declining is their words about why they cannot
    /// take a job. It belongs on the booking, not in an email subject line.
    /// </summary>
    [Fact]
    public async Task A_decline_is_reported_without_the_caregivers_reason()
    {
        var (requester, caregiver, booking) = await RequestedBookingAsync();

        const string reason = "I am already working for another family that evening.";

        (await caregiver.PostAsJsonAsync(
            $"/api/v1/bookings/{booking.Id}/decline", new { reason }))
            .EnsureSuccessStatusCode();

        var declined = Assert.Single(
            await NotificationsAsync(requester), n => n.Type == "BookingDeclined");

        Assert.DoesNotContain("already working", declined.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("already working", declined.Body ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The one that matters most in the set. A caregiver cancelling the evening before
    /// leaves a family without care in the morning, and until now they would have found
    /// out by opening the app.
    /// </summary>
    [Fact]
    public async Task A_family_is_told_when_the_caregiver_cancels()
    {
        var (requester, caregiver, booking) = await RequestedBookingAsync();

        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
            .EnsureSuccessStatusCode();

        (await caregiver.PostAsJsonAsync(
            $"/api/v1/bookings/{booking.Id}/cancel", new { reason = "I am unwell." }))
            .EnsureSuccessStatusCode();

        Assert.Contains(await NotificationsAsync(requester), n => n.Type == "BookingCancelled");
    }

    /// <summary>And the other way: a caregiver hears when the family cancels on them.</summary>
    [Fact]
    public async Task A_caregiver_is_told_when_the_family_cancels()
    {
        var (requester, caregiver, booking) = await RequestedBookingAsync();

        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
            .EnsureSuccessStatusCode();

        (await requester.PostAsJsonAsync(
            $"/api/v1/bookings/{booking.Id}/cancel", new { reason = "Plans changed." }))
            .EnsureSuccessStatusCode();

        var notifications = await NotificationsAsync(caregiver);

        Assert.Contains(notifications, n => n.Type == "BookingCancelled");

        // And the person who cancelled is not told about their own cancellation.
        Assert.DoesNotContain(
            await NotificationsAsync(requester), n => n.Type == "BookingCancelled");
    }

    [Fact]
    public async Task A_family_is_told_when_the_booking_is_finished()
    {
        var (requester, caregiver, booking) = await RequestedBookingAsync();

        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
            .EnsureSuccessStatusCode();

        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - Start);

        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null))
            .EnsureSuccessStatusCode();
        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-out", null))
            .EnsureSuccessStatusCode();

        Assert.Contains(await NotificationsAsync(requester), n => n.Type == "BookingCompleted");
    }

    /// <summary>
    /// A dispute stops the caregiver's payout. Money silently ceasing to arrive, with no
    /// message and no date, is the worst version of this — so both sides are told, the
    /// disputing family included.
    /// </summary>
    [Fact]
    public async Task Both_sides_are_told_when_a_booking_is_disputed()
    {
        var (requester, caregiver, booking) = await RequestedBookingAsync();

        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
            .EnsureSuccessStatusCode();

        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - Start);

        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null))
            .EnsureSuccessStatusCode();
        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-out", null))
            .EnsureSuccessStatusCode();

        const string reason = "The caregiver left two hours early without telling me.";

        (await requester.PostAsJsonAsync(
            $"/api/v1/bookings/{booking.Id}/dispute", new { reason }))
            .EnsureSuccessStatusCode();

        var toCaregiver = Assert.Single(
            await NotificationsAsync(caregiver), n => n.Type == "BookingDisputed");

        Assert.Contains(
            await NotificationsAsync(requester), n => n.Type == "BookingDisputed");

        // The complaint itself stays on the booking. This lands on a lock screen.
        Assert.DoesNotContain("two hours early", toCaregiver.Body ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
