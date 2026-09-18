using GoSit.Application.Features.CareRequesters;
using GoSit.Domain.Caregivers;

namespace GoSit.Application.Features.Caregivers;

public sealed record AvailabilityWindowDto(DayOfWeek Day, TimeOnly StartTime, TimeOnly EndTime);

public sealed record CaregiverCertificationResponse(
    Guid Id,
    string Code,
    string Name,
    DateOnly IssuedOn,
    DateOnly? ExpiresOn,
    string Status,
    bool IsValid);

public sealed record CaregiverInsurancePolicyResponse(
    Guid Id,
    string CoverageType,
    string Insurer,
    string PolicyReference,
    decimal LiabilityLimit,
    DateOnly EffectiveOn,
    DateOnly ExpiresOn,
    string Status,
    bool IsValid);

/// <summary>
/// The caregiver's own view of their profile. Includes the exact address and the
/// review state of each certification, neither of which appears in the public view
/// that search and profile pages will use.
/// </summary>
public sealed record CaregiverProfileResponse(
    Guid Id,
    string? Bio,
    int YearsOfExperience,
    decimal HourlyRate,
    decimal? NightlyRate,
    AddressDto? Address,
    int ServiceRadiusKm,
    string VerificationStatus,
    bool AcceptingWork,
    bool AvailableForLastMinuteRequests,
    bool IsListable,
    IReadOnlyList<AvailabilityWindowDto> Availability,
    IReadOnlyList<CaregiverCertificationResponse> Certifications,
    IReadOnlyList<CaregiverInsurancePolicyResponse> InsurancePolicies,
    IReadOnlyList<string> ServiceCodes,

    /// <summary>The agreement version this caregiver has signed, or null if never signed.</summary>
    string? AgreementAcceptedVersion,
    DateTime? AgreementAcceptedAtUtc,

    /// <summary>
    /// Whether that signature covers the version in force today. False either because the
    /// caregiver has never signed, or because the agreement was revised since they did —
    /// see <see cref="ServiceAgreement.CurrentVersion"/>.
    /// </summary>
    bool HasAcceptedCurrentAgreement,

    /// <summary>The version a caregiver would be signing if they accept right now.</summary>
    string CurrentAgreementVersion);

public static class CaregiverMapping
{
    public static CaregiverProfileResponse ToOwnerResponse(this CaregiverProfile profile, DateOnly today) =>
        new(
            profile.Id,
            profile.Bio,
            profile.YearsOfExperience,
            profile.HourlyRate,
            profile.NightlyRate,
            profile.Address?.ToDto(),
            profile.ServiceRadiusKm,
            profile.VerificationStatus.ToString(),
            profile.AcceptingWork,
            profile.AvailableForLastMinuteRequests,
            profile.IsListable,
            profile.Availability
                .OrderBy(s => s.Day)
                .ThenBy(s => s.StartTime)
                .Select(s => new AvailabilityWindowDto(s.Day, s.StartTime, s.EndTime))
                .ToList(),
            profile.Certifications
                .Select(c => c.ToResponse(today))
                .ToList(),
            profile.InsurancePolicies
                .Select(p => p.ToResponse(today))
                .ToList(),
            profile.Services.Select(s => s.Code).OrderBy(c => c).ToList(),
            profile.AgreementAcceptedVersion,
            profile.AgreementAcceptedAtUtc,
            profile.HasAcceptedCurrentAgreement,
            ServiceAgreement.CurrentVersion);

    public static CaregiverCertificationResponse ToResponse(
        this CaregiverCertification certification,
        DateOnly today) =>
        new(
            certification.Id,
            certification.Certification.Code,
            certification.Certification.Name,
            certification.IssuedOn,
            certification.ExpiresOn,
            certification.Status.ToString(),
            certification.IsValidOn(today));
    // DocumentReference is deliberately absent: it is a private Blob Storage key and
    // must never leave the server.

    public static CaregiverInsurancePolicyResponse ToResponse(
        this CaregiverInsurancePolicy policy,
        DateOnly today) =>
        new(
            policy.Id,
            policy.CoverageType.ToString(),
            policy.Insurer,
            policy.PolicyReference,
            policy.LiabilityLimit,
            policy.EffectiveOn,
            policy.ExpiresOn,
            policy.Status.ToString(),
            policy.IsValidOn(today));
    // DocumentReference is deliberately absent here too, for the same reason.
}
