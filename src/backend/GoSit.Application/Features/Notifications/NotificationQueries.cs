using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Messaging;

namespace GoSit.Application.Features.Notifications;

/// <summary>
/// Notifications for the signed-in account. Always scoped to the caller's user id —
/// there is no route that takes a user id, so one account cannot read another's.
/// </summary>
public sealed record ListNotificationsQuery(bool UnreadOnly, int Take = 50)
    : IQuery<NotificationListResponse>;

public sealed record NotificationListResponse(
    IReadOnlyList<NotificationResponse> Notifications,
    int UnreadCount);

public sealed record MarkNotificationReadCommand(Guid NotificationId) : ICommand<Unit>;

public sealed record MarkAllNotificationsReadCommand : ICommand<Unit>;

public sealed class ListNotificationsHandler(
    INotificationRepository notifications,
    ICurrentUser currentUser) : IQueryHandler<ListNotificationsQuery, NotificationListResponse>
{
    private const int MaxTake = 100;

    public async Task<NotificationListResponse> HandleAsync(
        ListNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var take = Math.Clamp(query.Take, 1, MaxTake);

        var results = await notifications.ListForUserAsync(
            userId, query.UnreadOnly, take, cancellationToken);

        var unreadCount = await notifications.CountUnreadAsync(userId, cancellationToken);

        return new NotificationListResponse(
            results
                .Select(n => new NotificationResponse(
                    n.Id,
                    n.Type.ToString(),
                    n.Title,
                    n.Body,
                    n.RelatedEntityType,
                    n.RelatedEntityId,
                    n.CreatedAtUtc,
                    n.IsRead))
                .ToList(),
            unreadCount);
    }
}

public sealed class MarkNotificationReadHandler(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<MarkNotificationReadCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        MarkNotificationReadCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var notification = await notifications.FindByIdAsync(command.NotificationId, cancellationToken);

        // A notification belonging to another account is reported as not found, so ids
        // cannot be probed and read state cannot be altered across accounts.
        if (notification is null || notification.UserId != userId)
        {
            throw new NotFoundException("That notification does not exist.");
        }

        notification.MarkRead(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class MarkAllNotificationsReadHandler(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<MarkAllNotificationsReadCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        MarkAllNotificationsReadCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        await notifications.MarkAllReadAsync(userId, clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
