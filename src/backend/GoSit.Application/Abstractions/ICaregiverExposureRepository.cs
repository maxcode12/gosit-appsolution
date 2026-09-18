namespace GoSit.Application.Abstractions;

/// <summary>Impressions and profile views for one caregiver over a window.</summary>
public sealed record CaregiverExposureTotals(int Impressions, int ProfileViews);

/// <summary>
/// Recording and reading how often caregivers were shown.
///
/// Writes are counted, never itemised: nothing here takes the identity of whoever was
/// searching, so the store cannot answer "which families looked at this caregiver" no
/// matter who asks it later. See <see cref="Domain.Caregivers.CaregiverExposure"/>.
/// </summary>
public interface ICaregiverExposureRepository
{
    /// <summary>
    /// Adds one impression for each caregiver shown on a page of results.
    ///
    /// Takes the whole page rather than one id at a time so a search costs one write
    /// regardless of page size. Duplicates within the list are counted once — a
    /// caregiver cannot appear twice on a page, and treating a repeat as two impressions
    /// would reward a bug with a better number.
    /// </summary>
    Task RecordImpressionsAsync(
        IReadOnlyCollection<Guid> caregiverProfileIds,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task RecordProfileViewAsync(
        Guid caregiverProfileId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<CaregiverExposureTotals> TotalsSinceAsync(
        Guid caregiverProfileId,
        DateOnly since,
        CancellationToken cancellationToken);
}
