using GoSit.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GoSit.Api.Realtime;

/// <summary>
/// Real-time channel for messaging and notifications.
///
/// Authorized, and clients are grouped by their own user id, so a connection can only
/// ever receive events addressed to the account that opened it. The hub has no methods
/// a client can call to fetch data — everything still goes through the authorized HTTP
/// endpoints, and the hub only announces that something changed.
/// </summary>
[Authorize]
public sealed class MessagingHub : Hub
{
    public static string GroupFor(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.GetUserId() is { } userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.User?.GetUserId() is { } userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(userId));
        }

        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Pushes real-time updates over SignalR.
///
/// Payloads carry identifiers and counts only, never a message body — the same payload
/// reaches every device the account has open, and a body has no business on all of them.
/// </summary>
public sealed class SignalRNotifier(IHubContext<MessagingHub> hub) : IRealtimeNotifier
{
    public Task NotifyMessageReceivedAsync(
        Guid recipientUserId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken) =>
        hub.Clients
            .Group(MessagingHub.GroupFor(recipientUserId))
            .SendAsync(
                "messageReceived",
                new { conversationId, messageId },
                cancellationToken);

    public Task NotifyNotificationCreatedAsync(
        Guid recipientUserId,
        Guid notificationId,
        int unreadCount,
        CancellationToken cancellationToken) =>
        hub.Clients
            .Group(MessagingHub.GroupFor(recipientUserId))
            .SendAsync(
                "notificationCreated",
                new { notificationId, unreadCount },
                cancellationToken);
}
