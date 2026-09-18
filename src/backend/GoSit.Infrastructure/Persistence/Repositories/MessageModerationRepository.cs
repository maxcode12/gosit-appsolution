using GoSit.Application.Abstractions;
using GoSit.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

/// <summary>
/// The moderation queue for messages flagged as possible contact sharing.
///
/// See <see cref="IMessageModerationRepository"/> for why the list projection carries no
/// message bodies. That is not an oversight to be tidied up later by adding a preview:
/// the queue is a list of strangers' private conversations, and it is built so that using
/// it does not mean reading them.
///
/// The name lookups are written inline rather than factored into helpers, because a
/// helper is a method call and EF cannot translate one inside a projection — it either
/// throws or, worse, silently evaluates per row on the client.
/// </summary>
public sealed class MessageModerationRepository(GoSitDbContext context)
    : IMessageModerationRepository
{
    public async Task<IReadOnlyList<FlaggedMessageRow>> ListAwaitingModerationAsync(
        int take,
        CancellationToken cancellationToken)
    {
        return await AwaitingModeration()
            // Oldest first. A flag sitting here is either someone being wrongly suspected
            // or someone taking care off-platform, and both get worse with time.
            .OrderBy(m => m.SentAtUtc)
            .Take(take)
            .Select(m => new FlaggedMessageRow(
                m.Id,
                m.ConversationId,
                m.SentByCaregiver
                    ? context.Conversations
                        .Where(c => c.Id == m.ConversationId)
                        .Join(context.CaregiverProfiles, c => c.CaregiverProfileId, p => p.Id, (c, p) => p.UserId)
                        .Join(context.Users, id => id, u => u.Id, (id, u) => u.FirstName + " " + u.LastName)
                        .FirstOrDefault()
                    : context.Conversations
                        .Where(c => c.Id == m.ConversationId)
                        .Join(context.CareRequesterProfiles, c => c.CareRequesterProfileId, p => p.Id, (c, p) => p.UserId)
                        .Join(context.Users, id => id, u => u.Id, (id, u) => u.FirstName + " " + u.LastName)
                        .FirstOrDefault(),
                m.SentByCaregiver
                    ? context.Conversations
                        .Where(c => c.Id == m.ConversationId)
                        .Join(context.CareRequesterProfiles, c => c.CareRequesterProfileId, p => p.Id, (c, p) => p.UserId)
                        .Join(context.Users, id => id, u => u.Id, (id, u) => u.FirstName + " " + u.LastName)
                        .FirstOrDefault()
                    : context.Conversations
                        .Where(c => c.Id == m.ConversationId)
                        .Join(context.CaregiverProfiles, c => c.CaregiverProfileId, p => p.Id, (c, p) => p.UserId)
                        .Join(context.Users, id => id, u => u.Id, (id, u) => u.FirstName + " " + u.LastName)
                        .FirstOrDefault(),
                m.SentByCaregiver,
                m.SentAtUtc))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAwaitingModerationAsync(CancellationToken cancellationToken) =>
        AwaitingModeration().CountAsync(cancellationToken);

    public Task<Message?> FindForReviewAsync(Guid messageId, CancellationToken cancellationToken) =>
        context.Set<Message>()
            .SingleOrDefaultAsync(m => m.Id == messageId && m.FlaggedForReview, cancellationToken);

    public async Task<FlaggedMessageParties?> FindPartiesAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var pair = await context.Set<Message>()
            .AsNoTracking()
            .Where(m => m.Id == messageId)
            .Select(m => new
            {
                m.SentByCaregiver,
                Caregiver = context.Conversations
                    .Where(c => c.Id == m.ConversationId)
                    .Join(context.CaregiverProfiles, c => c.CaregiverProfileId, p => p.Id, (c, p) => p.UserId)
                    .Join(context.Users, id => id, u => u.Id,
                        (id, u) => new { UserId = u.Id, Name = u.FirstName + " " + u.LastName })
                    .FirstOrDefault(),
                Requester = context.Conversations
                    .Where(c => c.Id == m.ConversationId)
                    .Join(context.CareRequesterProfiles, c => c.CareRequesterProfileId, p => p.Id, (c, p) => p.UserId)
                    .Join(context.Users, id => id, u => u.Id,
                        (id, u) => new { UserId = u.Id, Name = u.FirstName + " " + u.LastName })
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (pair?.Caregiver is null || pair.Requester is null)
        {
            return null;
        }

        var sender = pair.SentByCaregiver ? pair.Caregiver : pair.Requester;
        var recipient = pair.SentByCaregiver ? pair.Requester : pair.Caregiver;

        return new FlaggedMessageParties(
            sender.UserId, recipient.UserId, sender.Name, recipient.Name);
    }

    private IQueryable<Message> AwaitingModeration() =>
        context.Set<Message>()
            .AsNoTracking()
            // Matches the partial index on FlaggedForReview.
            .Where(m => m.FlaggedForReview && m.ModerationOutcome == null);
}
