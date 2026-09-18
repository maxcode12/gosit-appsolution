namespace GoSit.Mobile.UI.Services;

/// <summary>A caregiver's own profile, as much of it as the mobile app needs.</summary>
public sealed record MobileCaregiverProfile(
    bool AcceptingWork,
    bool AvailableForLastMinuteRequests,
    bool IsListable,
    IReadOnlyList<MobileCaregiverCertification> Certifications,
    IReadOnlyList<MobileCaregiverInsurancePolicy> InsurancePolicies);

public sealed record MobileCaregiverCertification(string Name, string Status, bool IsValid);

public sealed record MobileCaregiverInsurancePolicy(string CoverageType, string Status, bool IsValid);

public sealed record MobilePayoutAccount(
    bool BillingConfigured,
    bool HasAccount,
    bool DetailsSubmitted,
    bool PayoutsEnabled,
    string? DisabledReason,
    decimal PendingBalance,
    string Currency);

public sealed record MobilePayoutOnboarding(string OnboardingUrl);

/// <summary>The caregiver's own profile switches — availability toggles, mainly.</summary>
public sealed class CaregiverProfileService(GoSitApiClient api)
{
    public Task<MobileCaregiverProfile?> GetAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<MobileCaregiverProfile>("/api/v1/caregivers/me", cancellationToken);

    public Task<MobileCaregiverProfile?> SetAvailableForLastMinuteRequestsAsync(
        bool available, CancellationToken cancellationToken = default) =>
        api.PutAsync<MobileCaregiverProfile>(
            "/api/v1/caregivers/me/available-now",
            new { availableForLastMinuteRequests = available },
            cancellationToken);

    public Task<MobilePayoutAccount?> GetPayoutAccountAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<MobilePayoutAccount>("/api/v1/caregivers/me/payout-account", cancellationToken);

    public Task<MobilePayoutOnboarding?> StartPayoutOnboardingAsync(
        CancellationToken cancellationToken = default) =>
        api.PostAsync<MobilePayoutOnboarding>(
            "/api/v1/caregivers/me/payout-account", null, cancellationToken);
}
