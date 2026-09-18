namespace GoSit.Domain.Caregivers;

/// <summary>
/// A certification that is about to lapse, or has, and what it costs the caregiver.
///
/// <see cref="AffectedServiceNames"/> may be empty — a caregiver can hold a certification
/// no service they offer requires. That is worth warning about anyway, since they may be
/// relying on it to add a service later, but it must not be described as lost work.
/// </summary>
public sealed record LapsingCertification(
    Guid CaregiverCertificationId,
    string CertificationName,
    DateOnly ExpiresOn,
    ExpiryNoticeStage Stage,
    IReadOnlyList<string> AffectedServiceNames);
