using GoSit.Application.Abstractions;
using GoSit.Domain.Messaging;
using GoSit.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

public sealed class ConversationRepository(GoSitDbContext context) : IConversationRepository
{
    public Task<Conversation?> FindByIdAsync(Guid conversationId, CancellationToken cancellationToken) =>
        context.Conversations
            .Include(c => c.Messages)
            .SingleOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

    public Task<Conversation?> FindBetweenAsync(
        Guid careRequesterProfileId,
        Guid caregiverProfileId,
        CancellationToken cancellationToken) =>
        context.Conversations
            .Include(c => c.Messages)
            .SingleOrDefaultAsync(
                c => c.CareRequesterProfileId == careRequesterProfileId
                    && c.CaregiverProfileId == caregiverProfileId,
                cancellationToken);

    /// <summary>
    /// Messages are included because unread counts and the last-message preview are
    /// computed on the aggregate. Fine at inbox scale; a per-conversation projection is
    /// the change to make if threads grow long.
    /// </summary>
    public async Task<IReadOnlyList<Conversation>> ListForParticipantAsync(
        Guid profileId,
        bool isCaregiver,
        CancellationToken cancellationToken) =>
        await context.Conversations
            .Include(c => c.Messages)
            .Where(c => isCaregiver
                ? c.CaregiverProfileId == profileId
                : c.CareRequesterProfileId == profileId)
            // Threads with no messages yet sort last rather than first.
            .OrderByDescending(c => c.LastMessageAtUtc ?? DateTime.MinValue)
            .ToListAsync(cancellationToken);

    public void Add(Conversation conversation) => context.Conversations.Add(conversation);
}

public sealed class NotificationRepository(GoSitDbContext context) : INotificationRepository
{
    public void Add(Notification notification) => context.Notifications.Add(notification);

    public async Task<IReadOnlyList<Notification>> ListForUserAsync(
        Guid userId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAtUtc == null);
        }

        return await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<Notification?> FindByIdAsync(Guid notificationId, CancellationToken cancellationToken) =>
        context.Notifications.SingleOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Notifications.CountAsync(
            n => n.UserId == userId && n.ReadAtUtc == null, cancellationToken);

    /// <summary>
    /// A set-based update rather than loading every row: clearing a large backlog should
    /// not depend on how many notifications have accumulated.
    /// </summary>
    public Task MarkAllReadAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) =>
        context.Notifications
            .Where(n => n.UserId == userId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(n => n.ReadAtUtc, utcNow),
                cancellationToken);
}

/// <summary>
/// Maps profile ids to accounts and display names.
///
/// Projects only the name fields. Loading whole User rows here would put email addresses
/// one careless projection away from a messaging response.
/// </summary>
public sealed class ProfileDirectory(GoSitDbContext context) : IProfileDirectory
{
    public async Task<Guid?> FindUserIdForCareRequesterAsync(
        Guid profileId,
        CancellationToken cancellationToken) =>
        await context.CareRequesterProfiles
            .Where(p => p.Id == profileId)
            .Select(p => (Guid?)p.UserId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<Guid?> FindUserIdForCaregiverAsync(
        Guid profileId,
        CancellationToken cancellationToken) =>
        await context.CaregiverProfiles
            .Where(p => p.Id == profileId)
            .Select(p => (Guid?)p.UserId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, CaregiverName>> ListProfileNamesAsync(
        IReadOnlyCollection<Guid> profileIds,
        CancellationToken cancellationToken)
    {
        if (profileIds.Count == 0)
        {
            return new Dictionary<Guid, CaregiverName>();
        }

        // A profile id belongs to one side or the other, so both are looked up and merged.
        var requesters = await context.CareRequesterProfiles
            .AsNoTracking()
            .Where(p => profileIds.Contains(p.Id))
            .Join(context.Users, p => p.UserId, u => u.Id,
                (p, u) => new { ProfileId = p.Id, u.FirstName, u.LastName })
            .ToListAsync(cancellationToken);

        var caregivers = await context.CaregiverProfiles
            .AsNoTracking()
            .Where(p => profileIds.Contains(p.Id))
            .Join(context.Users, p => p.UserId, u => u.Id,
                (p, u) => new { ProfileId = p.Id, u.FirstName, u.LastName })
            .ToListAsync(cancellationToken);

        return requesters
            .Concat(caregivers)
            .ToDictionary(x => x.ProfileId, x => new CaregiverName(x.FirstName, x.LastName));
    }
}
