namespace GoSit.Domain.Caregivers;

/// <summary>An insurance policy that is about to lapse, or has. Mirrors <see cref="LapsingCertification"/>.</summary>
public sealed record LapsingInsurancePolicy(
    Guid CaregiverInsurancePolicyId,
    InsuranceCoverageType CoverageType,
    DateOnly ExpiresOn,
    ExpiryNoticeStage Stage);
