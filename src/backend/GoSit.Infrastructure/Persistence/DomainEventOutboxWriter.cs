using System.Text.Json;
using GoSit.Domain.Common;
using GoSit.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence;

/// <summary>
/// Turns the domain events raised during a unit of work into outbox rows.
///
/// Called from <see cref="GoSitDbContext.SaveChangesAsync"/> before the write, so the
/// outbox rows land in the same transaction as the state change. That is the whole
/// point of the pattern: the two cannot diverge.
/// </summary>
internal static class DomainEventOutboxWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static void CaptureDomainEvents(
        this GoSitDbContext context,
        DateTime utcNow,
        BusinessMetrics? metrics = null)
    {
        var entitiesWithEvents = context.ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                context.OutboxMessages.Add(OutboxMessage.Create(
                    domainEvent.GetType().Name,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions),
                    utcNow));

                // Counted here for the same reason the outbox row is written here:
                // every meaningful state change passes through this one point.
                metrics?.RecordDomainEvent(domainEvent.GetType().Name);
            }

            // Cleared immediately so a second SaveChanges in the same request cannot
            // publish the same event twice.
            entity.ClearDomainEvents();
        }
    }
}
