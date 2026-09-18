using GoSit.Domain.Caregivers;

namespace GoSit.Application.Abstractions;

public interface ICaregiverProfileRepository
{
    /// <summary>
    /// Loads the caregiver profile owned by the given account, with availability and
    /// certifications. Scoped by user id for the same ownership reason as
    /// <see cref="ICareRequesterProfileRepository.FindByUserIdAsync"/>.
    /// </summary>
    Task<CaregiverProfile?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a caregiver by profile id, tracked. Used by staff decisions and rating
    /// recalculation, which act on a caregiver other than the caller.
    /// </summary>
    Task<CaregiverProfile?> FindByIdAsync(Guid caregiverProfileId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a caregiver by their connected-account reference, tracked. Used to apply
    /// account webhooks, which identify a caregiver only by that reference.
    /// </summary>
    Task<CaregiverProfile?> FindByPayoutAccountIdAsync(
        string payoutAccountId,
        CancellationToken cancellationToken);

    Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken);

    void Add(CaregiverProfile profile);
}

public interface ICertificationRepository
{
    /// <summary>Reference data lookup by stable code, e.g. "CPR".</summary>
    Task<Certification?> FindByCodeAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<Certification>> ListActiveAsync(CancellationToken cancellationToken);
}
