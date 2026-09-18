using System.Net;
using System.Net.Http.Json;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

public static class MessagingTestHelpers
{
    public static async Task<bool> IsMessageFlaggedAsync(this GoSitApiFactory factory, Guid messageId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.Messages
            .Where(m => m.Id == messageId)
            .Select(m => m.FlaggedForReview)
            .SingleAsync();
    }
}

[Collection(nameof(ApiCollection))]
public class NotificationTests(GoSitApiFactory factory)
{
    private sealed record NotificationBody(
        Guid Id,
        string Type,
        string Title,
        string? Body,
        string? RelatedEntityType,
        Guid? RelatedEntityId,
        DateTime CreatedAtUtc,
        bool IsRead);

    private sealed record NotificationListBody(
        IReadOnlyList<NotificationBody> Notifications,
        int UnreadCount);

    private sealed record MessageBody(
        Guid Id, Guid SenderProfileId, bool SentByMe, string Body, DateTime SentAtUtc);

    private async Task<HttpClient> RequesterAsync()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();
        return client;
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient requester, Guid caregiverProfileId, string body) =>
        requester.PostAsJsonAsync("/api/v1/conversations", new { caregiverProfileId, body });

    [Fact]
    public async Task Receiving_a_message_creates_a_notification_for_the_recipient()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();
        await SendAsync(requester, caregiverId, "Are you free Friday?");

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var list = await caregiver.GetFromJsonAsync<NotificationListBody>("/api/v1/notifications");

        var notification = Assert.Single(list!.Notifications);
        Assert.Equal("MessageReceived", notification.Type);
        Assert.Equal("Conversation", notification.RelatedEntityType);
        Assert.Equal(1, list.UnreadCount);
    }

    /// <summary>
    /// Notifications are delivered by email and push and land on lock screens, so the
    /// message body must never appear in one.
    /// </summary>
    [Fact]
    public async Task A_notification_never_contains_the_message_body()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();
        await SendAsync(requester, caregiverId, "Sam is allergic to peanuts and needs his inhaler.");

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var body = await caregiver.GetStringAsync("/api/v1/notifications");

        Assert.DoesNotContain("peanuts", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inhaler", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sam", body);
        // A summary naming who wrote is all a notification carries.
        Assert.Contains("New message from Ada", body);
    }

    [Fact]
    public async Task The_sender_does_not_get_a_notification_for_their_own_message()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();
        await SendAsync(requester, caregiverId, "Hello");

        var list = await requester.GetFromJsonAsync<NotificationListBody>("/api/v1/notifications");

        Assert.Empty(list!.Notifications);
        Assert.Equal(0, list.UnreadCount);
    }

    [Fact]
    public async Task A_notification_can_be_marked_read()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();
        await SendAsync(requester, caregiverId, "Hello");

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var list = await caregiver.GetFromJsonAsync<NotificationListBody>("/api/v1/notifications");
        var notificationId = list!.Notifications.Single().Id;

        var response = await caregiver.PostAsync($"/api/v1/notifications/{notificationId}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await caregiver.GetFromJsonAsync<NotificationListBody>("/api/v1/notifications");
        Assert.Equal(0, after!.UnreadCount);
        Assert.True(after.Notifications.Single().IsRead);
    }

    [Fact]
    public async Task All_notifications_can_be_marked_read_at_once()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();
        await SendAsync(requester, caregiverId, "One");
        await SendAsync(requester, caregiverId, "Two");

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var response = await caregiver.PostAsync("/api/v1/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await caregiver.GetFromJsonAsync<NotificationListBody>("/api/v1/notifications");
        Assert.Equal(0, after!.UnreadCount);
    }

    [Fact]
    public async Task Unread_filtering_works()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();
        await SendAsync(requester, caregiverId, "One");

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var list = await caregiver.GetFromJsonAsync<NotificationListBody>("/api/v1/notifications");
        await caregiver.PostAsync($"/api/v1/notifications/{list!.Notifications.Single().Id}/read", null);

        var unread = await caregiver.GetFromJsonAsync<NotificationListBody>(
            "/api/v1/notifications?unreadOnly=true");

        Assert.Empty(unread!.Notifications);
    }

    /// <summary>
    /// One account must not be able to read or alter another's notifications.
    /// </summary>
    [Fact]
    public async Task Notifications_are_private_to_their_owner()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await RequesterAsync();
        await SendAsync(requester, caregiverId, "Hello");

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var list = await caregiver.GetFromJsonAsync<NotificationListBody>("/api/v1/notifications");
        var notificationId = list!.Notifications.Single().Id;

        var stranger = await RequesterAsync();

        Assert.Empty((await stranger.GetFromJsonAsync<NotificationListBody>(
            "/api/v1/notifications"))!.Notifications);

        // 404 rather than 403, so notification ids cannot be probed.
        var response = await stranger.PostAsync($"/api/v1/notifications/{notificationId}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // The owner's notification is untouched.
        var after = await caregiver.GetFromJsonAsync<NotificationListBody>("/api/v1/notifications");
        Assert.Equal(1, after!.UnreadCount);
    }

    [Fact]
    public async Task Notifications_require_authentication()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
