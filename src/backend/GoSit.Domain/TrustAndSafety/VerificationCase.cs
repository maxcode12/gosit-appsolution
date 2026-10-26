using GoSit.Domain.Common;

namespace GoSit.Domain.TrustAndSafety;

public enum VerificationCheckType
{
    /// <summary>Government ID matched to the person.</summary>
    Identity = 1,

    /// <summary>Criminal record check.</summary>
    BackgroundCheck = 2,

    /// <summary>Vulnerable sector check — required to work with children or seniors.</summary>
    VulnerableSector = 3,

    /// <summary>References contacted and confirmed.</summary>
    References = 4,
}

public enum VerificationCaseStatus
{
    /// <summary>Submitted by the caregiver, waiting to be picked up.</summary>
    Submitted = 1,

    /// <summary>With a provider or a reviewer.</summary>
    InReview = 2,

    Approved = 3,
    Rejected = 4,

    /// <summary>Withdrawn or superseded; carries no weight either way.</summary>
    Cancelled = 5,
}

/// <summary>
/// One trust-and-safety check on a caregiver.
///
/// A caregiver can start a case and supply evidence, but nothing they do can approve it.
/// <see cref="Approve"/> and <see cref="Reject"/> both require the reviewing staff
/// member's id and are reachable only from admin-gated handlers — a caregiver verifying
/// themselves would defeat the entire point of the marketplace.
///
/// <see cref="DocumentReference"/> is a private Blob Storage key. It must never appear in
/// a response, a log, or an event.
/// </summary>
public sealed class VerificationCase : Entity
{
    private VerificationCase()
    {
    }

    public Guid CaregiverProfileId { get; private set; }

    public VerificationCheckType CheckType { get; private set; }

    public VerificationCaseStatus Status { get; private set; }

    /// <summary>Blob Storage key for submitted evidence. Never exposed.</summary>
    public string? DocumentReference { get; private set; }

    /// <summary>Reference at the background-check provider, once one is wired up.</summary>
    public string? ProviderReference { get; private set; }

    /// <summary>
    /// Where the provider says the check has got to, in its own words. Evidence for the
    /// reviewer, never a decision: only staff can approve or reject a case.
    /// </summary>
    public string? ProviderStatus { get; private set; }

    /// <summary>
    /// The provider's verdict once it has one (for example "CLEAR" or "REVIEW"). Shown to
    /// staff beside the case. Deliberately not the report itself, which stays with the
    /// provider.
    /// </summary>
    public string? ProviderScore { get; private set; }

    public DateTime? ProviderUpdatedAtUtc { get; private set; }

    /// <summary>Staff member who decided. Null until a decision is made.</summary>
    public Guid? ReviewedByUserId { get; private set; }

    /// <summary>Internal note. Visible to staff and, for a rejection, to the caregiver.</summary>
    public string? DecisionNotes { get; private set; }

    public DateTime SubmittedAtUtc { get; private set; }

    public DateTime? ReviewedAtUtc { get; private set; }

    /// <summary>When an approval stops counting. Null means it does not expire.</summary>
    public DateOnly? ValidUntil { get; private set; }

    public int Version { get; private set; }

    public bool IsDecided => Status is VerificationCaseStatus.Approved
        or VerificationCaseStatus.Rejected or VerificationCaseStatus.Cancelled;

    public bool IsValidOn(DateOnly date) =>
        Status == VerificationCaseStatus.Approved
        && (ValidUntil is null || date <= ValidUntil);

    public static VerificationCase Submit(
        Guid caregiverProfileId,
        VerificationCheckType checkType,
        string? documentReference,
        DateTime utcNow)
    {
        if (caregiverProfileId == Guid.Empty)
        {
            throw new DomainException("A caregiver is required.");
        }

        return new VerificationCase
        {
            CaregiverProfileId = caregiverProfileId,
            CheckType = checkType,
            // Always starts unverified, whatever the caregiver claims.
            Status = VerificationCaseStatus.Submitted,
            DocumentReference = documentReference,
            SubmittedAtUtc = utcNow,
        };
    }

    /// <summary>Picked up by staff, or sent to a background-check provider.</summary>
    public void BeginReview(string? providerReference, DateTime utcNow)
    {
        if (Status != VerificationCaseStatus.Submitted)
        {
            throw new DomainException($"A case that is {Status} cannot be moved into review.");
        }

        Status = VerificationCaseStatus.InReview;
        ProviderReference = Trim(providerReference, 255);
        Version++;
        _ = utcNow;
    }

    /// <summary>
    /// Notes what the provider has reported. Changes nothing about the case's status: a
    /// clear result still waits for a staff member to approve it.
    ///
    /// Returns false, and records nothing, once the case has been decided. A late message
    /// from the provider must not rewrite the evidence a decision was made on.
    /// </summary>
    public bool RecordProviderUpdate(string? status, string? score, DateTime utcNow)
    {
        if (IsDecided)
        {
            return false;
        }

        ProviderStatus = Trim(status, 64);
        ProviderScore = Trim(score, 32);
        ProviderUpdatedAtUtc = utcNow;
        Version++;

        return true;
    }

    /// <summary>Staff decision only. The caregiver has no path to this.</summary>
    public void Approve(Guid reviewedByUserId, DateOnly? validUntil, string? notes, DateTime utcNow)
    {
        EnsureUndecided();

        if (reviewedByUserId == Guid.Empty)
        {
            throw new DomainException("A reviewer is required.");
        }

        if (validUntil is { } expiry && expiry <= DateOnly.FromDateTime(utcNow))
        {
            throw new DomainException("An approval cannot expire in the past.");
        }

        Status = VerificationCaseStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        ValidUntil = validUntil;
        DecisionNotes = Trim(notes, 1000);
        ReviewedAtUtc = utcNow;
        Version++;
    }

    public void Reject(Guid reviewedByUserId, string reason, DateTime utcNow)
    {
        EnsureUndecided();

        if (reviewedByUserId == Guid.Empty)
        {
            throw new DomainException("A reviewer is required.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A rejection reason is required.");
        }

        Status = VerificationCaseStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        DecisionNotes = Trim(reason, 1000);
        ReviewedAtUtc = utcNow;
        Version++;
    }

    public void Cancel(DateTime utcNow)
    {
        EnsureUndecided();

        Status = VerificationCaseStatus.Cancelled;
        ReviewedAtUtc = utcNow;
        Version++;
    }

    private void EnsureUndecided()
    {
        if (IsDecided)
        {
            throw new DomainException($"A case that is {Status} has already been decided.");
        }
    }

    private static string? Trim(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, maxLength)];
}

/// <summary>
/// Which checks a caregiver must pass before GoSit will list them.
///
/// Separate from the aggregate because it is a platform policy, not a property of one
/// case, and because Trust and Safety will want to tighten it without touching the
/// verification workflow.
/// </summary>
public static class VerificationRequirements
{
    /// <summary>
    /// The bar for caring for people: photo ID and a vulnerable sector check, the floor for
    /// anyone working with children or vulnerable adults.
    /// </summary>
    public static readonly VerificationCheckType[] RequiredForPeopleCare =
    [
        VerificationCheckType.Identity,
        VerificationCheckType.VulnerableSector,
    ];

    /// <summary>
    /// The bar for a caregiver who only offers pet care: photo ID and a criminal record
    /// check. They still enter homes and hold keys, so a record check is warranted; a
    /// vulnerable sector check exists for positions of trust over vulnerable people.
    /// </summary>
    public static readonly VerificationCheckType[] RequiredForPetCare =
    [
        VerificationCheckType.Identity,
        VerificationCheckType.BackgroundCheck,
    ];

    /// <summary>Whether a decision on this kind of check can change what a caregiver is cleared for.</summary>
    public static bool AffectsClearance(VerificationCheckType checkType) =>
        checkType is VerificationCheckType.Identity
            or VerificationCheckType.BackgroundCheck
            or VerificationCheckType.VulnerableSector;

    /// <summary>
    /// What a caregiver's currently valid checks clear them for.
    ///
    /// A vulnerable sector check includes a criminal record check, so it satisfies the pet
    /// bar too — a caregiver cleared for people is never asked for a second check to walk a
    /// dog.
    /// </summary>
    public static VerificationClearance ClearanceFrom(IEnumerable<VerificationCase> cases, DateOnly date)
    {
        var valid = cases
            .Where(c => c.IsValidOn(date))
            .Select(c => c.CheckType)
            .ToHashSet();

        if (!valid.Contains(VerificationCheckType.Identity))
        {
            return VerificationClearance.None;
        }

        if (valid.Contains(VerificationCheckType.VulnerableSector))
        {
            return VerificationClearance.PeopleCare;
        }

        return valid.Contains(VerificationCheckType.BackgroundCheck)
            ? VerificationClearance.PetCare
            : VerificationClearance.None;
    }

    /// <summary>The checks a caregiver needs for what they offer.</summary>
    public static IReadOnlyList<VerificationCheckType> RequiredFor(bool offersOnlyPetCare) =>
        offersOnlyPetCare ? RequiredForPetCare : RequiredForPeopleCare;

    public static bool AreSatisfiedBy(
        IEnumerable<VerificationCase> cases,
        DateOnly date,
        bool offersOnlyPetCare)
    {
        var clearance = ClearanceFrom(cases, date);

        return offersOnlyPetCare
            ? clearance != VerificationClearance.None
            : clearance == VerificationClearance.PeopleCare;
    }
}

/// <summary>What a caregiver's checks clear them to do.</summary>
public enum VerificationClearance
{
    None = 0,

    /// <summary>Photo ID and a criminal record check: pet care only.</summary>
    PetCare = 1,

    /// <summary>Photo ID and a vulnerable sector check: people and pets.</summary>
    PeopleCare = 2,
}
