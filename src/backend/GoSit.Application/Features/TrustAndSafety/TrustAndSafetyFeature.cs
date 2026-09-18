using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.TrustAndSafety;
using GoSit.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.TrustAndSafety;

// ---- reporting: available to any signed-in user ----

public sealed record ReportIncidentCommand(
    Guid SubjectUserId,
    Guid? CareBookingId,
    IncidentCategory Category,
    IncidentSeverity Severity,
    string Description) : ICommand<IncidentReportResponse>;

/// <summary>
/// What a reporter sees back. Deliberately excludes the resolution notes and the
/// assigned staff member — those are internal.
/// </summary>
public sealed record IncidentReportResponse(
    Guid Id,
    string Category,
    string Severity,
    string Status,
    DateTime CreatedAtUtc);

public sealed class ReportIncidentValidator : AbstractValidator<ReportIncidentCommand>
{
    public ReportIncidentValidator()
    {
        RuleFor(x => x.SubjectUserId).NotEmpty();
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Severity).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
    }
}

public sealed class ReportIncidentHandler(
    IIncidentRepository incidents,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<ReportIncidentHandler> logger)
    : ICommandHandler<ReportIncidentCommand, IncidentReportResponse>
{
    public async Task<IncidentReportResponse> HandleAsync(
        ReportIncidentCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        _ = await users.FindByIdAsync(command.SubjectUserId, cancellationToken)
            ?? throw new NotFoundException("That account does not exist.");

        var utcNow = clock.UtcNow;

        var report = IncidentReport.Raise(
            userId,
            command.SubjectUserId,
            command.CareBookingId,
            command.Category,
            command.Severity,
            command.Description,
            utcNow);

        incidents.Add(report);
        audit.Record(nameof(IncidentReport), report.Id, "IncidentReported", userId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Identifiers and classification only. The description is a user's account of
        // something that may involve a child and must never be logged.
        logger.LogWarning(
            "Incident {IncidentReportId} reported: {Category}/{Severity}, urgent={Urgent}",
            report.Id, report.Category, report.Severity, report.RequiresUrgentReview);

        return new IncidentReportResponse(
            report.Id,
            report.Category.ToString(),
            report.Severity.ToString(),
            report.Status.ToString(),
            report.CreatedAtUtc);
    }
}

// ---- verification: submitted by caregivers, decided by staff ----

public sealed record SubmitVerificationCommand(
    VerificationCheckType CheckType,
    string? DocumentReference) : ICommand<VerificationCaseResponse>;

/// <summary>
/// A verification case as shown to the caregiver who owns it.
///
/// The document reference is a private Blob Storage key and is deliberately absent.
/// Decision notes are included only for a rejection, so the caregiver knows what to fix.
/// </summary>
public sealed record VerificationCaseResponse(
    Guid Id,
    string CheckType,
    string Status,
    DateOnly? ValidUntil,
    string? DecisionNotes,
    DateTime SubmittedAtUtc,
    DateTime? ReviewedAtUtc);

public sealed record ListMyVerificationsQuery : IQuery<VerificationSummaryResponse>;

public sealed record VerificationSummaryResponse(
    string VerificationStatus,
    bool MeetsListingRequirements,

    /// <summary>
    /// The checks this caregiver needs, which depend on what they offer: photo ID and a
    /// vulnerable sector check to care for people, photo ID and a criminal record check for
    /// someone offering only pet care.
    /// </summary>
    IReadOnlyList<string> RequiredChecks,

    /// <summary>Whether their checks clear them for people as well as pets.</summary>
    bool ClearedForPeopleCare,
    IReadOnlyList<VerificationCaseResponse> Cases);

public sealed class SubmitVerificationValidator : AbstractValidator<SubmitVerificationCommand>
{
    public SubmitVerificationValidator()
    {
        RuleFor(x => x.CheckType).IsInEnum();
        RuleFor(x => x.DocumentReference).MaximumLength(500);
    }
}

public sealed class SubmitVerificationHandler(
    IVerificationRepository verifications,
    ICaregiverProfileRepository caregiverProfiles,
    IBackgroundCheckProvider backgroundChecks,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<SubmitVerificationCommand, VerificationCaseResponse>
{
    public async Task<VerificationCaseResponse> HandleAsync(
        SubmitVerificationCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var caregiver = await caregiverProfiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your caregiver profile first.");

        var utcNow = clock.UtcNow;

        var existing = await verifications.ListForCaregiverAsync(caregiver.Id, cancellationToken);

        // A check already approved and still valid does not need resubmitting, and an
        // undecided one is already in the queue.
        if (existing.Any(c => c.CheckType == command.CheckType
            && (c.IsValidOn(DateOnly.FromDateTime(utcNow)) || !c.IsDecided)))
        {
            throw new ConflictException("That check has already been submitted.");
        }

        var verificationCase = VerificationCase.Submit(
            caregiver.Id, command.CheckType, command.DocumentReference, utcNow);

        // A real provider would take it from here. Until one is chosen the case simply
        // waits for a staff decision — see IBackgroundCheckProvider.
        if (backgroundChecks.IsConfigured
            && command.CheckType is VerificationCheckType.BackgroundCheck
                or VerificationCheckType.VulnerableSector)
        {
            var reference = await backgroundChecks.InitiateAsync(
                caregiver.Id, command.CheckType, cancellationToken);

            verificationCase.BeginReview(reference, utcNow);
        }

        verifications.Add(verificationCase);

        // The caregiver is now awaiting a decision rather than simply unverified.
        caregiver.MarkVerificationPending(utcNow);

        audit.Record(nameof(VerificationCase), verificationCase.Id, "VerificationSubmitted", userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return verificationCase.ToResponse();
    }
}

public sealed class ListMyVerificationsHandler(
    IVerificationRepository verifications,
    ICaregiverProfileRepository caregiverProfiles,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IQueryHandler<ListMyVerificationsQuery, VerificationSummaryResponse>
{
    public async Task<VerificationSummaryResponse> HandleAsync(
        ListMyVerificationsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var caregiver = await caregiverProfiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("No caregiver profile has been created yet.");

        var cases = await verifications.ListForCaregiverAsync(caregiver.Id, cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow);

        var offersOnlyPetCare = caregiver.OffersOnlyPetCare;

        return new VerificationSummaryResponse(
            caregiver.VerificationStatus.ToString(),
            VerificationRequirements.AreSatisfiedBy(cases, today, offersOnlyPetCare),
            VerificationRequirements.RequiredFor(offersOnlyPetCare).Select(t => t.ToString()).ToList(),
            caregiver.ClearedForPeopleCare,
            cases.Select(c => c.ToResponse()).ToList());
    }
}

public static class VerificationMapping
{
    public static VerificationCaseResponse ToResponse(this VerificationCase c) =>
        new(
            c.Id,
            c.CheckType.ToString(),
            c.Status.ToString(),
            c.ValidUntil,
            // Shown only for a rejection, so the caregiver knows what to fix. An
            // approval's internal notes stay internal.
            c.Status == VerificationCaseStatus.Rejected ? c.DecisionNotes : null,
            c.SubmittedAtUtc,
            c.ReviewedAtUtc);
    // DocumentReference is deliberately absent: it is a private Blob Storage key.
}
