using GoSit.Application.Abstractions;
using GoSit.Domain.Audit;
using GoSit.Domain.Bookings;
using GoSit.Domain.CareRecipients;
using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Pets;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository(GoSitDbContext context) : IBookingRepository
{
    /// <summary>
    /// Statuses that hold a slot in the caregiver's calendar. A declined or cancelled
    /// booking releases the time; a completed one is in the past.
    /// </summary>
    private static readonly BookingStatus[] LiveStatuses =
    [
        BookingStatus.Pending,
        BookingStatus.Accepted,
        BookingStatus.InProgress,
    ];

    public Task<CareBooking?> FindByIdAsync(Guid bookingId, CancellationToken cancellationToken) =>
        context.CareBookings
            .Include(b => b.Recipients)
            .Include(b => b.Pets)
            .SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

    public async Task<IReadOnlyList<CareBooking>> ListForCareRequesterAsync(
        Guid careRequesterProfileId,
        BookingListFilter filter,
        DateTime utcNow,
        CancellationToken cancellationToken) =>
        await Filtered(
                context.CareBookings.Where(b => b.CareRequesterProfileId == careRequesterProfileId),
                filter,
                utcNow)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CareBooking>> ListForCaregiverAsync(
        Guid caregiverProfileId,
        BookingListFilter filter,
        DateTime utcNow,
        CancellationToken cancellationToken) =>
        await Filtered(
                context.CareBookings.Where(b => b.CaregiverProfileId == caregiverProfileId),
                filter,
                utcNow)
            .ToListAsync(cancellationToken);

    public Task<bool> HasOverlappingBookingAsync(
        Guid caregiverProfileId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        Guid? excludingBookingId,
        CancellationToken cancellationToken) =>
        context.CareBookings
            .Where(b => b.CaregiverProfileId == caregiverProfileId)
            .Where(b => LiveStatuses.Contains(b.Status))
            .Where(b => excludingBookingId == null || b.Id != excludingBookingId)
            // Half-open intervals: a booking ending exactly when another starts is fine.
            .AnyAsync(b => b.StartAtUtc < endAtUtc && startAtUtc < b.EndAtUtc, cancellationToken);

    public void Add(CareBooking booking) => context.CareBookings.Add(booking);

    /// <summary>
    /// Tracked, with everything eligibility needs. Accept re-evaluates certifications
    /// and availability, so those collections have to be loaded.
    /// </summary>
    public Task<CaregiverProfile?> FindCaregiverForBookingAsync(
        Guid caregiverProfileId,
        CancellationToken cancellationToken) =>
        context.CaregiverProfiles
            .Include(p => p.Availability)
            .Include(p => p.Services)
            .Include(p => p.Certifications)
            .ThenInclude(c => c.Certification)
            .SingleOrDefaultAsync(p => p.Id == caregiverProfileId, cancellationToken);

    public Task<CareService?> FindServiceByIdAsync(
        Guid careServiceId,
        CancellationToken cancellationToken) =>
        context.CareServices.SingleOrDefaultAsync(s => s.Id == careServiceId, cancellationToken);

    private static IQueryable<CareBooking> Filtered(
        IQueryable<CareBooking> query,
        BookingListFilter filter,
        DateTime utcNow)
    {
        // Pets as well as recipients: a list is where a sitter first sees which animal a job is for.
        query = query.Include(b => b.Recipients).Include(b => b.Pets);

        if (filter.Status is { } status)
        {
            query = query.Where(b => b.Status == status);
        }

        if (filter.UpcomingOnly)
        {
            query = query.Where(b => b.EndAtUtc >= utcNow);

            // Soonest first when looking ahead...
            return query.OrderBy(b => b.StartAtUtc);
        }

        // ...most recent first otherwise, which is what a history list wants.
        return query.OrderByDescending(b => b.StartAtUtc);
    }
}

public sealed class BookingLookupRepository(GoSitDbContext context) : IBookingLookupRepository
{
    public async Task<IReadOnlyList<CareRecipient>> ListRecipientsAsync(
        IReadOnlyCollection<Guid> careRecipientIds,
        CancellationToken cancellationToken)
    {
        if (careRecipientIds.Count == 0)
        {
            return [];
        }

        return await context.CareRecipients
            .AsNoTracking()
            .Include(r => r.CareNeeds)
            .Where(r => careRecipientIds.Contains(r.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Pet>> ListPetsAsync(
        IReadOnlyCollection<Guid> petIds,
        CancellationToken cancellationToken)
    {
        if (petIds.Count == 0)
        {
            return [];
        }

        return await context.Set<Pet>()
            .AsNoTracking()
            .Where(p => petIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, CareService>> ListServicesAsync(
        IReadOnlyCollection<Guid> careServiceIds,
        CancellationToken cancellationToken)
    {
        if (careServiceIds.Count == 0)
        {
            return new Dictionary<Guid, CareService>();
        }

        var services = await context.CareServices
            .AsNoTracking()
            .Where(s => careServiceIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        return services.ToDictionary(s => s.Id);
    }

    public Task<DateTime?> FindCompletedAtAsync(
        Guid careBookingId,
        CancellationToken cancellationToken) =>
        context.CareBookings
            .AsNoTracking()
            .Where(b => b.Id == careBookingId)
            .Select(b => b.CompletedAtUtc)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Guid>> ListCaregiverUserIdsAsync(
        IReadOnlyCollection<Guid> caregiverProfileIds,
        CancellationToken cancellationToken)
    {
        if (caregiverProfileIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var pairs = await context.CaregiverProfiles
            .AsNoTracking()
            .Where(p => caregiverProfileIds.Contains(p.Id))
            .Select(p => new { p.Id, p.UserId })
            .ToListAsync(cancellationToken);

        return pairs.ToDictionary(p => p.Id, p => p.UserId);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> ListCareRequesterUserIdsAsync(
        IReadOnlyCollection<Guid> careRequesterProfileIds,
        CancellationToken cancellationToken)
    {
        if (careRequesterProfileIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var pairs = await context.CareRequesterProfiles
            .AsNoTracking()
            .Where(p => careRequesterProfileIds.Contains(p.Id))
            .Select(p => new { p.Id, p.UserId })
            .ToListAsync(cancellationToken);

        return pairs.ToDictionary(p => p.Id, p => p.UserId);
    }
}

/// <summary>
/// Writes audit rows into the current unit of work, so they commit with the change they
/// describe rather than in a separate transaction that could be lost.
/// </summary>
public sealed class AuditTrail(
    GoSitDbContext context,
    IDateTimeProvider clock,
    ICorrelationContext correlation) : IAuditTrail
{
    public void Record(string entityType, Guid entityId, string action, Guid? actorUserId) =>
        context.AuditEvents.Add(AuditEvent.Record(
            entityType, entityId, action, actorUserId, clock.UtcNow, correlation.CorrelationId));
}
