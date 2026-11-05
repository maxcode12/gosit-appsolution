using GoSit.Domain.Caregivers;

namespace GoSit.IntegrationTests.Api;

internal static class InsuranceTestExtensions
{
    /// <summary>
    /// Gives the caregiver the verified liability policy that listing and booking require —
    /// see <see cref="CaregiverProfile.HasRequiredInsurance"/> — as staff review would.
    /// </summary>
    public static void GiveVerifiedInsurance(this CaregiverProfile profile, DateTime utcNow)
    {
        var today = DateOnly.FromDateTime(utcNow);

        var policy = profile.SubmitInsurance(
            InsuranceCoverageType.GeneralLiability,
            "Test Insurer",
            "TEST-POLICY",
            2_000_000m,
            today.AddYears(-1),
            today.AddYears(1),
            utcNow);

        profile.AttachInsuranceDocument(policy.Id, "test/policy.pdf", utcNow);
        profile.ApproveInsurance(policy.Id, utcNow);
    }
}
