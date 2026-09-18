using GoSit.Application.Abstractions;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// Notifications for the signed-in account.
///
/// No route takes a user id: everything is scoped to the caller's token, so one account
/// can neither read nor alter another's notifications.
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController(
    IQueryHandler<ListNotificationsQuery, NotificationListResponse> list,
    ICommandHandler<MarkNotificationReadCommand, Unit> markRead,
    ICommandHandler<MarkAllNotificationsReadCommand, Unit> markAllRead) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(NotificationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationListResponse>> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await list.HandleAsync(new ListNotificationsQuery(unreadOnly, take), cancellationToken));

    [HttpPost("{notificationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        await markRead.HandleAsync(new MarkNotificationReadCommand(notificationId), cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await markAllRead.HandleAsync(new MarkAllNotificationsReadCommand(), cancellationToken);
        return NoContent();
    }
}
