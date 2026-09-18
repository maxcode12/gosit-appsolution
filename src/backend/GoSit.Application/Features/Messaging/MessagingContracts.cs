namespace GoSit.Application.Features.Messaging;

/// <summary>
/// A conversation as it appears in an inbox.
///
/// Carries the other participant's first name and last initial only — never their email,
/// phone number, or address. GoSit deliberately does not disclose contact details
/// through messaging; the thread is the channel.
/// </summary>
public sealed record ConversationSummary(
    Guid ConversationId,
    Guid OtherParticipantProfileId,
    string OtherParticipantFirstName,
    string OtherParticipantLastInitial,
    string? LastMessagePreview,
    DateTime? LastMessageAtUtc,
    int UnreadCount);

public sealed record MessageResponse(
    Guid Id,
    Guid SenderProfileId,
    bool SentByMe,
    string Body,
    DateTime SentAtUtc);

public sealed record ConversationDetail(
    Guid ConversationId,
    Guid OtherParticipantProfileId,
    string OtherParticipantFirstName,
    string OtherParticipantLastInitial,
    IReadOnlyList<MessageResponse> Messages,
    bool HasMore);

/// <summary>
/// An in-app notification. The body is a short summary safe to show on a lock screen —
/// message content and care details never appear here.
/// </summary>
public sealed record NotificationResponse(
    Guid Id,
    string Type,
    string Title,
    string? Body,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTime CreatedAtUtc,
    bool IsRead);
