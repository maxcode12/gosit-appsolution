using GoSit.Domain.Common;

namespace GoSit.Domain.Caregivers;

/// <summary>
/// A certification a caregiver claims to hold, plus the evidence and its review state.
///
/// <see cref="DocumentReference"/> is a private Blob Storage key. It must never be
/// returned to a care requester — only the derived status and expiry are public.
/// </summary>
public sealed class CaregiverCertification : Entity
{
    private CaregiverCertification()
    {
    }

    public Guid CaregiverProfileId { get; private set; }

    public Guid CertificationId { get; private set; }

    public Certification Certification { get; private set; } = null!;

    public DateOnly IssuedOn { get; private set; }

    public DateOnly? ExpiresOn { get; private set; }

    public CertificationStatus Status { get; private set; }

    /// <summary>Blob Storage key for the uploaded document. Never exposed publicly.</summary>
    public string? DocumentReference { get; private set; }

    public string? ReviewNotes { get; private set; }

    public DateTime SubmittedAtUtc { get; private set; }

    public DateTime? ReviewedAtUtc { get; private set; }

    /// <summary>
    /// How far the caregiver has been warned about this certification lapsing.
    ///
    /// Advances one way only, so a sweeper running every quarter hour warns once at each
    /// stage rather than every cycle. It is on the certification rather than derived from
    /// the notifications table because a caregiver who deletes a notification must not be
    /// told again, and because renewal has to be able to reset it.
    /// </summary>
    public ExpiryNoticeStage ExpiryNoticeStage { get; private set; }

    public bool IsExpiredOn(DateOnly date) => ExpiresOn is { } expiry && date > expiry;

    /// <summary>
    /// The stage this certification warrants on the given date, or null when there is
    /// nothing to say — no expiry, not yet verified, or still comfortably in date.
    ///
    /// A pending or rejected certification is deliberately silent: it is not earning the
    /// caregiver any work, so its lapsing costs them nothing and a warning would only be
    /// noise on top of a rejection they have already been told about.
    /// </summary>
    public ExpiryNoticeStage? NoticeDueOn(DateOnly date, int daysAhead)
    {
        if (Status != CertificationStatus.Verified || ExpiresOn is not { } expiry)
        {
            return null;
        }

        var due = date > expiry
            ? ExpiryNoticeStage.Expired
            : date >= expiry.AddDays(-daysAhead)
                ? ExpiryNoticeStage.Expiring
                : (ExpiryNoticeStage?)null;

        // Only ever forward: someone warned that it expired is not warned again that it
        // is about to.
        return due > ExpiryNoticeStage ? due : null;
    }

    internal void MarkExpiryNoticed(ExpiryNoticeStage stage)
    {
        if (stage > ExpiryNoticeStage)
        {
            ExpiryNoticeStage = stage;
        }
    }

    /// <summary>
    /// Whether this certification counts towards eligibility. A pending, rejected, or
    /// expired certification never does.
    /// </summary>
    public bool IsValidOn(DateOnly date) =>
        Status == CertificationStatus.Verified && !IsExpiredOn(date);

    internal static CaregiverCertification Submit(
        Guid caregiverProfileId,
        Certification certification,
        DateOnly issuedOn,
        DateOnly? expiresOn,
        DateTime utcNow)
    {
        var today = DateOnly.FromDateTime(utcNow);

        if (issuedOn > today)
        {
            throw new DomainException("An issue date cannot be in the future.");
        }

        if (certification.RequiresExpiry && expiresOn is null)
        {
            throw new DomainException($"{certification.Name} requires an expiry date.");
        }

        if (expiresOn is { } expiry)
        {
            if (expiry <= issuedOn)
            {
                throw new DomainException("An expiry date must be after the issue date.");
            }

            if (expiry <= today)
            {
                throw new DomainException("That certification has already expired.");
            }
        }

        return new CaregiverCertification
        {
            CaregiverProfileId = caregiverProfileId,
            CertificationId = certification.Id,
            Certification = certification,
            IssuedOn = issuedOn,
            ExpiresOn = expiresOn,
            // Always starts unverified, whatever the caregiver claims.
            Status = CertificationStatus.Pending,
            SubmittedAtUtc = utcNow,
        };
    }

    /// <summary>Whether there is anything for a reviewer to actually look at.</summary>
    public bool HasDocument => !string.IsNullOrWhiteSpace(DocumentReference);

    /// <summary>
    /// Records where the uploaded evidence was stored.
    ///
    /// Internal, and the key is assigned by the document store rather than supplied by
    /// the caregiver — see <see cref="Application.Abstractions.IDocumentStore"/> for why
    /// a caller-chosen key would let one caregiver cite another's record check.
    /// </summary>
    internal void AttachDocument(string documentReference)
    {
        if (string.IsNullOrWhiteSpace(documentReference))
        {
            throw new DomainException("A document reference is required.");
        }

        DocumentReference = documentReference;
    }

    /// <summary>
    /// Clears the review and sends it back to the queue.
    ///
    /// Replacing the evidence after a decision has to undo the decision. Otherwise a
    /// caregiver could be approved on one document and then quietly swap it for another,
    /// and the Verified badge would be attesting to a file nobody reviewed.
    /// </summary>
    internal void ReopenForReview()
    {
        Status = CertificationStatus.Pending;
        ReviewNotes = null;
        ReviewedAtUtc = null;
    }

    internal void Approve(DateTime utcNow)
    {
        /*
         * The rule this whole feature exists for.
         *
         * Approving a certification is what lets a caregiver work with children and
         * vulnerable adults, and until there was an upload, staff were approving from a
         * code and two dates the caregiver had typed themselves. The Verified badge on a
         * profile has to mean someone looked at something.
         */
        if (!HasDocument)
        {
            throw new DomainException(
                "This certification has no uploaded document, so there is nothing to verify. "
                + "Ask the caregiver to upload the certificate before approving it.");
        }

        Status = CertificationStatus.Verified;
        ReviewNotes = null;
        ReviewedAtUtc = utcNow;
    }

    internal void Reject(string reason, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A rejection reason is required.");
        }

        Status = CertificationStatus.Rejected;
        ReviewNotes = reason.Trim();
        ReviewedAtUtc = utcNow;
    }
}
