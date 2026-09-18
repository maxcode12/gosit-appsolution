namespace GoSit.Application.Abstractions;

/// <summary>
/// Reads across every caregiver's submitted certifications, for the staff review queue.
///
/// The caregiver profile repository is aggregate-scoped — it loads one profile at a time,
/// which is right for the caregiver's own screens and useless for a queue that spans all
/// of them. This is the query side of the same data.
/// </summary>
public interface ICertificationReviewRepository
{
    Task<IReadOnlyList<PendingCertificationRow>> ListPendingAsync(
        int take,
        CancellationToken cancellationToken);

    /// <summary>
    /// The profile that owns a certification, so a decision can be applied through the
    /// aggregate rather than by updating the row directly.
    /// </summary>
    Task<Guid?> FindOwningProfileIdAsync(
        Guid caregiverCertificationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Caregivers holding a verified certification that has lapsed, or lapses within
    /// <paramref name="daysAhead"/>, and who have not yet been warned about that stage.
    ///
    /// Returns profile ids rather than certification rows: the decision about what to say
    /// and the marking of it belong to the aggregate — see
    /// <see cref="Domain.Caregivers.CaregiverProfile.TakeExpiryNotices"/> — and this is
    /// only the query that finds who to load.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListProfileIdsWithLapsingCertificationsAsync(
        DateOnly today,
        int daysAhead,
        int take,
        CancellationToken cancellationToken);
}

public sealed record PendingCertificationRow(
    Guid CaregiverCertificationId,
    Guid CaregiverProfileId,
    string Code,
    string Name,
    DateOnly IssuedOn,
    DateOnly? ExpiresOn,

    /// <summary>Whether evidence was uploaded. The key never leaves the server.</summary>
    bool HasDocument,

    DateTime SubmittedAtUtc);
