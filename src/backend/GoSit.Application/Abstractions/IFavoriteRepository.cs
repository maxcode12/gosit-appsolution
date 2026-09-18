using GoSit.Domain.Caregivers;

namespace GoSit.Application.Abstractions;

public interface IFavoriteRepository
{
    Task<bool> ExistsAsync(
        Guid careRequesterProfileId,
        Guid caregiverProfileId,
        CancellationToken cancellationToken);

    /// <summary>How many caregivers this requester has saved. For the free-plan cap.</summary>
    Task<int> CountAsync(Guid careRequesterProfileId, CancellationToken cancellationToken);

    void Add(Guid careRequesterProfileId, Guid caregiverProfileId, DateTime utcNow);

    Task RemoveAsync(
        Guid careRequesterProfileId,
        Guid caregiverProfileId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The caregivers this requester has saved, newest first, with the collections the
    /// result projection needs.
    /// </summary>
    Task<IReadOnlyList<CaregiverProfile>> ListSavedCaregiversAsync(
        Guid careRequesterProfileId,
        CancellationToken cancellationToken);
}
