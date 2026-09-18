namespace GoSit.Domain.Caregivers;

/// <summary>
/// Trust and safety state of a caregiver. Only <see cref="Verified"/> may be surfaced
/// as a badge; the other values are internal and must not be shown to care requesters
/// as a reason a caregiver was excluded.
/// </summary>
public enum VerificationStatus
{
    Unverified = 1,
    Pending = 2,
    Verified = 3,
    Rejected = 4,
}
