using GoSit.Domain.Messaging;
using GoSit.Domain.Notifications;

namespace GoSit.Application.Abstractions;

public interface IConversationRepository
{
    /// <summary>Loads a conversation with its messages. Callers must check participation.</summary>
    Task<Conversation?> FindByIdAsync(Guid conversationId, CancellationToken cancellationToken);

    /// <summary>The existing thread between this pair, if there is one.</summary>
    Task<Conversation?> FindBetweenAsync(
        Guid careRequesterProfileId,
        Guid caregiverProfileId,
        CancellationToken cancellationToken);

    /// <summary>Conversations for one participant, most recent activity first.</summary>
    Task<IReadOnlyList<Conversation>> ListForParticipantAsync(
        Guid profileId,
        bool isCaregiver,
        CancellationToken cancellationToken);

    void Add(Conversation conversation);
}

public interface INotificationRepository
{
    void Add(Notification notification);

    Task<IReadOnlyList<Notification>> ListForUserAsync(
        Guid userId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken);

    Task<Notification?> FindByIdAsync(Guid notificationId, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Marks every unread notification for one account as read.</summary>
    Task MarkAllReadAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);
}

/// <summary>
/// Maps a profile id to the account that owns it, so a notification aimed at a
/// participant can be addressed to their user.
/// </summary>
public interface IProfileDirectory
{
    Task<Guid?> FindUserIdForCareRequesterAsync(Guid profileId, CancellationToken cancellationToken);

    Task<Guid?> FindUserIdForCaregiverAsync(Guid profileId, CancellationToken cancellationToken);

    /// <summary>Display names for a set of profile ids, whichever side they are on.</summary>
    Task<IReadOnlyDictionary<Guid, CaregiverName>> ListProfileNamesAsync(
        IReadOnlyCollection<Guid> profileIds,
        CancellationToken cancellationToken);
}

/// <summary>
/// Pushes a real-time update to a connected client. Implemented over SignalR.
///
/// Carries identifiers and counts only — never a message body — because the same
/// payload reaches every device a user has open.
/// </summary>
public interface IRealtimeNotifier
{
    Task NotifyMessageReceivedAsync(
        Guid recipientUserId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken);

    Task NotifyNotificationCreatedAsync(
        Guid recipientUserId,
        Guid notificationId,
        int unreadCount,
        CancellationToken cancellationToken);
}
