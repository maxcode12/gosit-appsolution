namespace GoSit.Domain.Caregivers;

/// <summary>
/// The version identifier of the Caregiver Service Agreement a caregiver must accept
/// before they can be listed.
///
/// This is a version stamp, not the agreement's text — the text is drafted and reviewed
/// as a legal document (see <c>docs/legal/Go-Sit-Caregiver-Service-Agreement-DRAFT.docx</c>
/// and <c>docs/BUSINESS_MODEL.md</c>) and is not yet lawyer-approved. Bumping this constant
/// is how a real revision of that document, once approved, requires every caregiver to
/// sign again: <see cref="CaregiverProfile.HasAcceptedCurrentAgreement"/> compares a
/// caregiver's recorded acceptance against whatever this says today.
/// </summary>
public static class ServiceAgreement
{
    /// <summary>
    /// OPEN BUSINESS/LEGAL DECISION: this identifies a draft, not a document counsel has
    /// signed off on. Do not present this acceptance to a caregiver as though it were a
    /// finished, binding contract until the underlying document has been through legal
    /// review — see docs/BUSINESS_MODEL.md, "Open legal questions."
    /// </summary>
    public const string CurrentVersion = "2026-09-15-draft";
}
