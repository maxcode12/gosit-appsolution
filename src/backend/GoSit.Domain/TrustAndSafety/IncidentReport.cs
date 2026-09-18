using GoSit.Domain.Common;
using GoSit.Domain.TrustAndSafety.Events;

namespace GoSit.Domain.TrustAndSafety;

public enum IncidentCategory
{
    /// <summary>Harm, or risk of harm, to a care recipient. Always the highest priority.</summary>
    SafetyConcern = 1,
    Harassment = 2,
    Fraud = 3,
    NoShow = 4,
    PropertyDamage = 5,
    InappropriateConduct = 6,
    Other = 7,
}

public enum IncidentSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,

    /// <summary>Someone may be in immediate danger. Escalates outside the queue.</summary>
    Critical = 4,
}

public enum IncidentStatus
{
    Open = 1,
    UnderReview = 2,
    Resolved = 3,

    /// <summary>Reviewed and found not to need action.</summary>
    Dismissed = 4,
}

/// <summary>
/// A report raised by one user about another.
///
/// The description is written by a user about a real person and may name a child or
/// describe a medical situation. Treat it as sensitive: never logged, never shown to the
/// reported user, and never returned to anyone but the reporter and staff.
///
/// A report is never deleted. It is the evidence trail for a safety decision.
/// </summary>
public sealed class IncidentReport : Entity
{
    private const int MaxDescriptionLength = 4000;

    private IncidentReport()
    {
    }

    /// <summary>Account that raised the report.</summary>
    public Guid ReportedByUserId { get; private set; }

    /// <summary>Account the report is about.</summary>
    public Guid SubjectUserId { get; private set; }

    /// <summary>The booking it relates to, when there is one.</summary>
    public Guid? CareBookingId { get; private set; }

    public IncidentCategory Category { get; private set; }

    public IncidentSeverity Severity { get; private set; }

    public IncidentStatus Status { get; private set; }

    /// <summary>The reporter's account. Sensitive — see the note on this class.</summary>
    public string Description { get; private set; } = string.Empty;

    public Guid? AssignedToUserId { get; private set; }

    /// <summary>What staff did about it. Staff-visible only.</summary>
    public string? ResolutionNotes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public int Version { get; private set; }

    /// <summary>
    /// Whether this needs attention now rather than in queue order. Trust and Safety
    /// paging and dashboards key off this.
    /// </summary>
    public bool RequiresUrgentReview =>
        Status is IncidentStatus.Open or IncidentStatus.UnderReview
        && (Severity == IncidentSeverity.Critical || Category == IncidentCategory.SafetyConcern);

    public static IncidentReport Raise(
        Guid reportedByUserId,
        Guid subjectUserId,
        Guid? careBookingId,
        IncidentCategory category,
        IncidentSeverity severity,
        string description,
        DateTime utcNow)
    {
        if (reportedByUserId == Guid.Empty || subjectUserId == Guid.Empty)
        {
            throw new DomainException("A reporter and a subject are required.");
        }

        if (reportedByUserId == subjectUserId)
        {
            throw new DomainException("A report cannot be raised about yourself.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("A description is required.");
        }

        var trimmed = description.Trim();

        if (trimmed.Length > MaxDescriptionLength)
        {
            throw new DomainException(
                $"A description must be {MaxDescriptionLength} characters or fewer.");
        }

        var report = new IncidentReport
        {
            ReportedByUserId = reportedByUserId,
            SubjectUserId = subjectUserId,
            CareBookingId = careBookingId,
            Category = category,
            Severity = severity,
            Status = IncidentStatus.Open,
            Description = trimmed,
            CreatedAtUtc = utcNow,
        };

        // Carries identifiers and the classification only — never the description.
        report.RaiseDomainEvent(new IncidentReportedDomainEvent(
            report.Id, subjectUserId, category, severity, report.RequiresUrgentReview, utcNow));

        return report;
    }

    public void Assign(Guid staffUserId)
    {
        if (Status is IncidentStatus.Resolved or IncidentStatus.Dismissed)
        {
            throw new DomainException($"A report that is {Status} cannot be reassigned.");
        }

        if (staffUserId == Guid.Empty)
        {
            throw new DomainException("A staff member is required.");
        }

        AssignedToUserId = staffUserId;
        Status = IncidentStatus.UnderReview;
        Version++;
    }

    public void Resolve(Guid staffUserId, string notes, DateTime utcNow) =>
        Close(IncidentStatus.Resolved, staffUserId, notes, utcNow);

    public void Dismiss(Guid staffUserId, string notes, DateTime utcNow) =>
        Close(IncidentStatus.Dismissed, staffUserId, notes, utcNow);

    private void Close(IncidentStatus status, Guid staffUserId, string notes, DateTime utcNow)
    {
        if (Status is IncidentStatus.Resolved or IncidentStatus.Dismissed)
        {
            throw new DomainException("This report has already been closed.");
        }

        if (staffUserId == Guid.Empty)
        {
            throw new DomainException("A staff member is required.");
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            // An outcome with no reasoning is not an audit trail.
            throw new DomainException("Closing a report requires an explanation.");
        }

        Status = status;
        AssignedToUserId = staffUserId;
        ResolutionNotes = notes.Trim()[..Math.Min(notes.Trim().Length, 2000)];
        ResolvedAtUtc = utcNow;
        Version++;
    }
}
