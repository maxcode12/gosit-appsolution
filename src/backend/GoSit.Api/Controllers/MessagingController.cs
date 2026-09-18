using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Messaging;
using GoSit.Application.Features.Messaging.Queries;
using GoSit.Application.Features.Messaging.SendMessage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// Private conversations between a care requester and a caregiver.
///
/// Used by both sides, so it is not gated by a role policy — each handler resolves which
/// side the caller is on. A conversation the caller is not part of returns 404, never
/// 403, so ids cannot be probed.
///
/// Responses carry the other participant's first name and last initial only. GoSit does
/// not disclose email addresses or phone numbers through messaging.
/// </summary>
[ApiController]
[Route("api/v1/conversations")]
[Authorize]
public sealed class MessagingController(
    ICommandHandler<SendMessageCommand, MessageResponse> send,
    ICommandHandler<ReplyToConversationCommand, MessageResponse> reply,
    ICommandHandler<MarkConversationReadCommand, Unit> markRead,
    IQueryHandler<ListConversationsQuery, IReadOnlyList<ConversationSummary>> listConversations,
    IQueryHandler<GetConversationQuery, ConversationDetail> getConversation) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConversationSummary>>> List(
        CancellationToken cancellationToken) =>
        Ok(await listConversations.HandleAsync(new ListConversationsQuery(), cancellationToken));

    /// <summary>Reads a thread. Opening it marks it read for the caller.</summary>
    [HttpGet("{conversationId:guid}")]
    [ProducesResponseType(typeof(ConversationDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDetail>> Get(
        Guid conversationId,
        [FromQuery] int take = 50,
        [FromQuery] DateTime? before = null,
        CancellationToken cancellationToken = default) =>
        Ok(await getConversation.HandleAsync(
            new GetConversationQuery(conversationId, take, before), cancellationToken));

    /// <summary>
    /// Starts a conversation with a caregiver, or adds to the existing one.
    /// Care requesters only — a caregiver replies rather than initiating.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Authentication.AuthorizationPolicies.CareRequesterOnly)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageResponse>> Send(
        [FromBody] SendMessageRequest request,
        [FromServices] IValidator<SendMessageCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new SendMessageCommand(request.CaregiverProfileId, request.Body ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await send.HandleAsync(command, cancellationToken));
    }

    [HttpPost("{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageResponse>> Reply(
        Guid conversationId,
        [FromBody] ReplyRequest request,
        [FromServices] IValidator<ReplyToConversationCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new ReplyToConversationCommand(conversationId, request.Body ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await reply.HandleAsync(command, cancellationToken));
    }

    [HttpPost("{conversationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid conversationId, CancellationToken cancellationToken)
    {
        await markRead.HandleAsync(new MarkConversationReadCommand(conversationId), cancellationToken);
        return NoContent();
    }
}

public sealed record SendMessageRequest(Guid CaregiverProfileId, string? Body);

public sealed record ReplyRequest(string? Body);
