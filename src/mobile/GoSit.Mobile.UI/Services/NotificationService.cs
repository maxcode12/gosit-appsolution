namespace GoSit.Mobile.UI.Services;

public sealed record MobileNotification(
    Guid Id,
    string Type,
    string Title,
    string? Body,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTime CreatedAtUtc,
    bool IsRead);

public sealed record MobileNotificationList(IReadOnlyList<MobileNotification> Notifications, int UnreadCount);

/// <summary>Notifications — new bookings, interviews, expiring paperwork, and the rest.</summary>
public sealed class NotificationService(GoSitApiClient api)
{
    public Task<MobileNotificationList?> ListAsync(
        bool unreadOnly = false, CancellationToken cancellationToken = default) =>
        api.GetAsync<MobileNotificationList>(
            $"/api/v1/notifications?unreadOnly={unreadOnly}", cancellationToken);

    public Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default) =>
        api.PostAsync<object>($"/api/v1/notifications/{notificationId}/read", null, cancellationToken);

    public Task MarkAllReadAsync(CancellationToken cancellationToken = default) =>
        api.PostAsync<object>("/api/v1/notifications/read-all", null, cancellationToken);
}
