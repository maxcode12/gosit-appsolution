using GoSit.Domain.Common;

namespace GoSit.Domain.Caregivers;

/// <summary>
/// How often one caregiver was shown to families on one day.
///
/// This is the only thing GoSit records about search behaviour, and the shape is the
/// privacy decision rather than an implementation detail.
///
/// A row is (caregiver, day, two counts). It carries **no searcher**. The obvious design
/// — a row per view, naming who looked — would answer richer questions and would also be
/// a standing record of which families are considering which caregivers. On a care
/// marketplace that is close to a record of who is looking for help with a child, a
/// parent, or a disability, and it is not worth a vanity metric. The same reasoning
/// already keeps filters off <c>BusinessMetrics.RecordSearch</c>.
///
/// Daily rather than a single running total because the figure is reported over a
/// ninety-day window: one lifetime counter cannot answer "how am I doing now", and per
/// event rows would be the behavioural log this exists to avoid.
///
/// Counts are advanced by an upsert in the repository rather than through this type.
/// Two families searching at once would otherwise contend on the same tracked row, and
/// a lost update on a page impression is not worth a transaction — see
/// <c>CaregiverExposureRepository</c>.
/// </summary>
public sealed class CaregiverExposure : Entity
{
    private CaregiverExposure()
    {
    }

    public Guid CaregiverProfileId { get; private set; }

    /// <summary>The UTC day these counts belong to.</summary>
    public DateOnly OnDate { get; private set; }

    /// <summary>
    /// Times this caregiver appeared in a page of results someone was actually shown.
    ///
    /// Being in the candidate set does not count. A caregiver on page four was not seen,
    /// and counting them would inflate the number that a caregiver is most likely to
    /// read as "how visible am I".
    /// </summary>
    public int Impressions { get; private set; }

    /// <summary>Times someone opened this caregiver's profile.</summary>
    public int ProfileViews { get; private set; }

    /// <summary>
    /// For tests and seeding. The production path is the repository's upsert; going
    /// through here would make every search a read-modify-write on a contended row.
    /// </summary>
    public static CaregiverExposure Create(
        Guid caregiverProfileId,
        DateOnly onDate,
        int impressions,
        int profileViews)
    {
        if (impressions < 0 || profileViews < 0)
        {
            throw new DomainException("An exposure count cannot be negative.");
        }

        return new CaregiverExposure
        {
            CaregiverProfileId = caregiverProfileId,
            OnDate = onDate,
            Impressions = impressions,
            ProfileViews = profileViews,
        };
    }
}
