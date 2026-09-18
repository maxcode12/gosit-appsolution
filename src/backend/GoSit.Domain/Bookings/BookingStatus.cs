namespace GoSit.Domain.Bookings;

/// <summary>
/// Lifecycle of a care booking. The permitted transitions are exactly those in
/// CLAUDE.md; see <see cref="CareBooking"/>, which is the only place they are applied.
///
/// Pending   -> Accepted | Declined | Cancelled
/// Accepted  -> InProgress | Cancelled
/// InProgress-> Completed
/// Completed -> Disputed
/// </summary>
public enum BookingStatus
{
    /// <summary>Requested by the care requester, awaiting the caregiver's answer.</summary>
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    Cancelled = 4,

    /// <summary>The caregiver has checked in and care is under way.</summary>
    InProgress = 5,
    Completed = 6,
    Disputed = 7,
}

/// <summary>Which side ended a booking. Recorded because the two carry different consequences.</summary>
public enum CancellationParty
{
    CareRequester = 1,
    Caregiver = 2,

    /// <summary>Cancelled by GoSit — for example when a caregiver is suspended.</summary>
    Platform = 3,
}
