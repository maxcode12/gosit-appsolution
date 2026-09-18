namespace GoSit.Domain.Caregivers;

public enum InsuranceVerificationStatus
{
    /// <summary>Submitted by the caregiver, not yet reviewed. Confers no eligibility.</summary>
    Pending = 1,
    Verified = 2,
    Rejected = 3,
}
