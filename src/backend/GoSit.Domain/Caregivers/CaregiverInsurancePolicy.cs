using GoSit.Domain.Common;

namespace GoSit.Domain.Caregivers;

/// <summary>
/// A liability policy a caregiver claims to hold, plus the evidence and its review state.
///
/// The marketplace model this mirrors is deliberate: GoSit does not insure caregivers —
/// see docs/BUSINESS_MODEL.md and docs/ADR-004-marketplace-provider-model.md — so a
/// caregiver's coverage is something GoSit verifies, the same way it verifies a
/// certification, never something GoSit issues or administers.
///
/// <see cref="DocumentReference"/> is a private Blob Storage key and must never be
/// returned to a care requester — only the derived status and expiry are public, and
/// even those only once product/legal decide what a family may see (see
/// docs/TRUST_AND_SAFETY.md, "Insurance verification workflow").
/// </summary>
public sealed class CaregiverInsurancePolicy : Entity
{
    private const int MaxInsurerLength = 200;
    private const int MaxPolicyReferenceLength = 100;
    private const int MaxReviewNotesLength = 1000;

    private CaregiverInsurancePolicy()
    {
    }

    public Guid CaregiverProfileId { get; private set; }

    public InsuranceCoverageType CoverageType { get; private set; }

    public string Insurer { get; private set; } = string.Empty;

    public string PolicyReference { get; private set; } = string.Empty;

    /// <summary>The policy's liability limit, in CAD.</summary>
    public decimal LiabilityLimit { get; private set; }

    public DateOnly EffectiveOn { get; private set; }

    public DateOnly ExpiresOn { get; private set; }

    public InsuranceVerificationStatus Status { get; private set; }

    /// <summary>Blob Storage key for the uploaded proof of insurance. Never exposed publicly.</summary>
    public string? DocumentReference { get; private set; }

    public string? ReviewNotes { get; private set; }

    public DateTime SubmittedAtUtc { get; private set; }

    public DateTime? ReviewedAtUtc { get; private set; }

    /// <summary>Mirrors <see cref="CaregiverCertification.ExpiryNoticeStage"/>: advances one way only.</summary>
    public ExpiryNoticeStage ExpiryNoticeStage { get; private set; }

    public bool HasDocument => !string.IsNullOrWhiteSpace(DocumentReference);

    public bool IsExpiredOn(DateOnly date) => date > ExpiresOn;

    public bool IsValidOn(DateOnly date) =>
        Status == InsuranceVerificationStatus.Verified && !IsExpiredOn(date) && date >= EffectiveOn;

    /// <summary>
    /// The stage this policy warrants on the given date, or null when there is nothing to
    /// say. Identical shape to <see cref="CaregiverCertification.NoticeDueOn"/>, including
    /// the 30-days/7-days-then-lapsed staging used across both — see
    /// docs/TRUST_AND_SAFETY.md, "Expiry handling."
    /// </summary>
    public ExpiryNoticeStage? NoticeDueOn(DateOnly date, int daysAhead)
    {
        if (Status != InsuranceVerificationStatus.Verified)
        {
            return null;
        }

        var due = date > ExpiresOn
            ? ExpiryNoticeStage.Expired
            : date >= ExpiresOn.AddDays(-daysAhead)
                ? ExpiryNoticeStage.Expiring
                : (ExpiryNoticeStage?)null;

        return due > ExpiryNoticeStage ? due : null;
    }

    internal void MarkExpiryNoticed(ExpiryNoticeStage stage)
    {
        if (stage > ExpiryNoticeStage)
        {
            ExpiryNoticeStage = stage;
        }
    }

    internal static CaregiverInsurancePolicy Submit(
        Guid caregiverProfileId,
        InsuranceCoverageType coverageType,
        string insurer,
        string policyReference,
        decimal liabilityLimit,
        DateOnly effectiveOn,
        DateOnly expiresOn,
        DateTime utcNow)
    {
        var trimmedInsurer = insurer?.Trim() ?? string.Empty;
        var trimmedReference = policyReference?.Trim() ?? string.Empty;

        if (trimmedInsurer.Length == 0)
        {
            throw new DomainException("An insurer name is required.");
        }

        if (trimmedInsurer.Length > MaxInsurerLength)
        {
            throw new DomainException($"An insurer name must be {MaxInsurerLength} characters or fewer.");
        }

        if (trimmedReference.Length == 0)
        {
            throw new DomainException("A policy reference is required.");
        }

        if (trimmedReference.Length > MaxPolicyReferenceLength)
        {
            throw new DomainException($"A policy reference must be {MaxPolicyReferenceLength} characters or fewer.");
        }

        if (liabilityLimit <= 0)
        {
            throw new DomainException("A liability limit must be greater than zero.");
        }

        if (expiresOn <= effectiveOn)
        {
            throw new DomainException("A policy's expiry date must be after its effective date.");
        }

        var today = DateOnly.FromDateTime(utcNow);

        if (expiresOn <= today)
        {
            throw new DomainException("That policy has already expired.");
        }

        return new CaregiverInsurancePolicy
        {
            CaregiverProfileId = caregiverProfileId,
            CoverageType = coverageType,
            Insurer = trimmedInsurer,
            PolicyReference = trimmedReference,
            LiabilityLimit = liabilityLimit,
            EffectiveOn = effectiveOn,
            ExpiresOn = expiresOn,
            // Always starts unverified, whatever the caregiver claims — the same rule as
            // a certification, and for the same reason: staff verify it, not the caregiver.
            Status = InsuranceVerificationStatus.Pending,
            SubmittedAtUtc = utcNow,
        };
    }

    internal void AttachDocument(string documentReference)
    {
        if (string.IsNullOrWhiteSpace(documentReference))
        {
            throw new DomainException("A document reference is required.");
        }

        DocumentReference = documentReference;
    }

    internal void ReopenForReview()
    {
        Status = InsuranceVerificationStatus.Pending;
        ReviewNotes = null;
        ReviewedAtUtc = null;
    }

    internal void Approve(DateTime utcNow)
    {
        if (!HasDocument)
        {
            throw new DomainException(
                "This policy has no uploaded proof of insurance, so there is nothing to verify. "
                + "Ask the caregiver to upload it before approving.");
        }

        Status = InsuranceVerificationStatus.Verified;
        ReviewNotes = null;
        ReviewedAtUtc = utcNow;
    }

    internal void Reject(string reason, DateTime utcNow)
    {
        var trimmed = reason?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new DomainException("A rejection reason is required.");
        }

        Status = InsuranceVerificationStatus.Rejected;
        ReviewNotes = trimmed.Length > MaxReviewNotesLength ? trimmed[..MaxReviewNotesLength] : trimmed;
        ReviewedAtUtc = utcNow;
    }
}
