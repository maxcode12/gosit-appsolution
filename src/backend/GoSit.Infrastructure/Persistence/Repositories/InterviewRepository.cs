using GoSit.Application.Abstractions;
using GoSit.Domain.Interviews;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

public sealed class InterviewRepository(GoSitDbContext context) : IInterviewRepository
{
    /// <summary>A participant's list is capped: it is a screen, not an archive.</summary>
    private const int ParticipantListLimit = 100;

    public Task<Interview?> FindByIdAsync(Guid interviewId, CancellationToken cancellationToken) =>
        context.Interviews
            .Include(i => i.Recordings)
            .SingleOrDefaultAsync(i => i.Id == interviewId, cancellationToken);

    public Task<Interview?> FindByProviderRecordingIdAsync(
        string providerRecordingId,
        CancellationToken cancellationToken) =>
        context.Interviews
            .Include(i => i.Recordings)
            .SingleOrDefaultAsync(
                i => i.Recordings.Any(r => r.ProviderRecordingId == providerRecordingId),
                cancellationToken);

    public Task<Interview?> FindByRecordingIdAsync(Guid recordingId, CancellationToken cancellationToken) =>
        context.Interviews
            .Include(i => i.Recordings)
            .SingleOrDefaultAsync(i => i.Recordings.Any(r => r.Id == recordingId), cancellationToken);

    public async Task<IReadOnlyList<Interview>> ListForParticipantAsync(
        Guid profileId,
        bool isCaregiver,
        Guid? otherProfileId,
        CancellationToken cancellationToken)
    {
        var query = context.Interviews
            .AsNoTracking()
            .Where(i => isCaregiver ? i.CaregiverProfileId == profileId : i.CareRequesterProfileId == profileId);

        if (otherProfileId is { } other)
        {
            query = query.Where(i => isCaregiver ? i.CareRequesterProfileId == other : i.CaregiverProfileId == other);
        }

        return await query
            .OrderByDescending(i => i.ScheduledStartUtc)
            .Take(ParticipantListLimit)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasOpenInterviewAsync(
        Guid careRequesterProfileId,
        Guid caregiverProfileId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var acceptedCutoff = utcNow - InterviewPolicy.LongestOpenSpan;

        return context.Interviews.AnyAsync(
            i => i.CareRequesterProfileId == careRequesterProfileId
                && i.CaregiverProfileId == caregiverProfileId
                && ((i.Status == InterviewStatus.Requested && i.ScheduledStartUtc > utcNow)
                    || (i.Status == InterviewStatus.Accepted && i.ScheduledStartUtc > acceptedCutoff)),
            cancellationToken);
    }

    public async Task<bool> TryClaimRecordingLeaseAsync(
        Guid interviewId,
        DateTime utcNow,
        DateTime leaseUntilUtc,
        CancellationToken cancellationToken) =>
        await context.Interviews
            .Where(i => i.Id == interviewId
                && (i.RecordingLeaseUntilUtc == null || i.RecordingLeaseUntilUtc < utcNow))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(i => i.RecordingLeaseUntilUtc, leaseUntilUtc),
                cancellationToken) == 1;

    public Task ReleaseRecordingLeaseAsync(Guid interviewId, CancellationToken cancellationToken) =>
        context.Interviews
            .Where(i => i.Id == interviewId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(i => i.RecordingLeaseUntilUtc, (DateTime?)null),
                cancellationToken);

    public async Task<IReadOnlyList<Interview>> ListRecordedAsync(int take, CancellationToken cancellationToken) =>
        await context.Interviews
            .AsNoTracking()
            .Include(i => i.Recordings)
            .Where(i => i.Recordings.Any())
            .OrderByDescending(i => i.ScheduledStartUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Interview>> ListWithRecordingsDueForDeletionAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken) =>
        await context.Interviews
            .Include(i => i.Recordings)
            .Where(i => !i.RecordingsOnHold
                && i.Recordings.Any(r =>
                    (r.Status == InterviewRecordingStatus.Available || r.Status == InterviewRecordingStatus.Failed)
                    && r.StorageKey != null
                    && r.DeleteAfterUtc <= utcNow))
            .OrderBy(i => i.ScheduledStartUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public void Add(Interview interview) => context.Interviews.Add(interview);
}
