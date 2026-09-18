using GoSit.Domain.Caregivers;

namespace GoSit.Application.Abstractions;

/// <summary>
/// Reads across every caregiver's submitted insurance policies, for the staff review
/// queue. Mirrors <see cref="ICertificationReviewRepository"/> for the same reason: the
/// caregiver profile repository is aggregate-scoped, and a queue spanning every caregiver
/// needs its own query side.
/// </summary>
public interface IInsuranceReviewRepository
{
    Task<IReadOnlyList<PendingInsuranceRow>> ListPendingAsync(int take, CancellationToken cancellationToken);

    /// <summary>The profile that owns a policy, so a decision is applied through the aggregate.</summary>
    Task<Guid?> FindOwningProfileIdAsync(Guid caregiverInsurancePolicyId, CancellationToken cancellationToken);

    /// <summary>
    /// Caregivers holding a verified policy that has lapsed, or lapses within
    /// <paramref name="daysAhead"/>, not yet warned about that stage. Mirrors
    /// <see cref="ICertificationReviewRepository.ListProfileIdsWithLapsingCertificationsAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListProfileIdsWithLapsingInsuranceAsync(
        DateOnly today,
        int daysAhead,
        int take,
        CancellationToken cancellationToken);
}

public sealed record PendingInsuranceRow(
    Guid CaregiverInsurancePolicyId,
    Guid CaregiverProfileId,
    InsuranceCoverageType CoverageType,
    string Insurer,
    string PolicyReference,
    decimal LiabilityLimit,
    DateOnly EffectiveOn,
    DateOnly ExpiresOn,

    /// <summary>Whether evidence was uploaded. The key never leaves the server.</summary>
    bool HasDocument,

    DateTime SubmittedAtUtc);
