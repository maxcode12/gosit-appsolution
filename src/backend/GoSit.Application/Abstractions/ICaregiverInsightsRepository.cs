namespace GoSit.Application.Abstractions;

/// <summary>
/// What happened to one of a caregiver's bookings, and when.
///
/// Carries no address, no instructions, no recipient and no requester name — insights
/// are counts, and loading the full aggregate to produce a count would pull a care
/// recipient's details into a screen that has no business showing them.
/// </summary>
public sealed record CaregiverBookingFact(
    Guid CareRequesterProfileId,
    BookingOutcome Status,
    DateTime CreatedAtUtc,
    DateTime? AcceptedAtUtc);

/// <summary>
/// How a request ended, from the caregiver's point of view.
///
/// Deliberately not <c>BookingStatus</c>. That enum cannot distinguish a request the
/// caregiver declined from one the platform closed because nobody answered, and telling
/// a caregiver they declined something they never saw would be worse than showing
/// nothing at all.
/// </summary>
public enum BookingOutcome
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    InProgress = 4,
    Completed = 5,
    CancelledByCaregiver = 6,
    CancelledByRequester = 7,

    /// <summary>Closed by the platform because it reached its start time unanswered.</summary>
    LapsedUnanswered = 8,

    Disputed = 9,
}

public interface ICaregiverInsightsRepository
{
    /// <summary>
    /// Every booking this caregiver has been named on, as bare facts.
    ///
    /// All of them rather than a window: the lifetime figures — completed work, families
    /// who came back — need the whole history, and a caregiver's booking count is small
    /// enough that one projection is cheaper than two queries.
    /// </summary>
    Task<IReadOnlyList<CaregiverBookingFact>> ListBookingFactsAsync(
        Guid caregiverProfileId,
        CancellationToken cancellationToken);
}
