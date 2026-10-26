using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.TrustAndSafety;
using GoSit.Domain.TrustAndSafety;
using GoSit.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Administration;

/// <summary>
/// Staff-only operations.
///
/// Everything here is gated by the AdminOnly policy at the route AND records who acted
/// in the audit trail. These are the decisions that let a stranger into a family's home,
/// so "who approved this and when" has to be answerable afterwards.
/// </summary>
public sealed record ApproveVerificationCommand(Guid CaseId, DateOnly? ValidUntil, string? Notes)
    : ICommand<VerificationCaseResponse>;

public sealed record RejectVerificationCommand(Guid CaseId, string Reason)
    : ICommand<VerificationCaseResponse>;

public sealed record ListPendingVerificationsQuery(int Take = 50)
    : IQuery<IReadOnlyList<PendingVerification>>;

/// <summary>
/// A check waiting on a decision.
///
/// Carries who the caregiver is, not just which profile: nobody can decide whether to
/// let a person into a family's home from a profile id. The owning account id is here
/// too, since suspending is the action a reviewer reaches for when a check comes back
/// badly, and it takes a user id.
/// </summary>
public sealed record PendingVerification(
    Guid CaseId,
    Guid CaregiverProfileId,
    Guid CaregiverUserId,
    string CaregiverName,
    string CaregiverEmail,
    string CaregiverAccountStatus,
    string CheckType,
    string Status,
    DateTime SubmittedAtUtc,

    /// <summary>What the screening provider says, if one is handling this check. Evidence only.</summary>
    string? ProviderStatus = null,
    string? ProviderScore = null);

public sealed record ListOpenIncidentsQuery(int Take = 50) : IQuery<IReadOnlyList<AdminIncident>>;

/// <summary>
/// A report as staff see it — including the description, which they need in order to
/// act, and which is why these routes are admin-gated.
/// </summary>
public sealed record AdminIncident(
    Guid Id,
    Guid ReportedByUserId,
    string ReportedByName,
    Guid SubjectUserId,
    string SubjectName,
    string SubjectEmail,
    string SubjectRole,
    /// <summary>Whether the subject is already suspended, so staff do not act twice.</summary>
    string SubjectAccountStatus,
    Guid? CareBookingId,
    string Category,
    string Severity,
    string Status,
    bool RequiresUrgentReview,
    string Description,
    Guid? AssignedToUserId,
    DateTime CreatedAtUtc);

public sealed record ResolveIncidentCommand(Guid ReportId, string Notes, bool Dismiss)
    : ICommand<Unit>;

public sealed record SuspendAccountCommand(Guid UserId, string Reason) : ICommand<Unit>;

public sealed record ReinstateAccountCommand(Guid UserId) : ICommand<Unit>;

public sealed class RejectVerificationValidator : AbstractValidator<RejectVerificationCommand>
{
    public RejectVerificationValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class ResolveIncidentValidator : AbstractValidator<ResolveIncidentCommand>
{
    public ResolveIncidentValidator()
    {
        RuleFor(x => x.ReportId).NotEmpty();
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(2000);
    }
}

public sealed class SuspendAccountValidator : AbstractValidator<SuspendAccountCommand>
{
    public SuspendAccountValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ApproveVerificationHandler(
    IVerificationRepository verifications,
    ICaregiverProfileRepository caregiverProfiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<ApproveVerificationHandler> logger)
    : ICommandHandler<ApproveVerificationCommand, VerificationCaseResponse>
{
    public async Task<VerificationCaseResponse> HandleAsync(
        ApproveVerificationCommand command,
        CancellationToken cancellationToken)
    {
        var staffId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var verificationCase = await verifications.FindByIdAsync(command.CaseId, cancellationToken)
            ?? throw new NotFoundException("That verification case does not exist.");

        var utcNow = clock.UtcNow;

        verificationCase.Approve(staffId, command.ValidUntil, command.Notes, utcNow);

        // A caregiver becomes verified only once every required check passes — approving
        // one check is not the same as clearing them to work.
        var caregiver = await caregiverProfiles.FindByIdAsync(
            verificationCase.CaregiverProfileId, cancellationToken);

        if (caregiver is not null)
        {
            var cases = await verifications.ListForCaregiverAsync(caregiver.Id, cancellationToken);

            var clearance = VerificationRequirements.ClearanceFrom(cases, DateOnly.FromDateTime(utcNow));

            // Verified for pets, or for people and pets — never "verified" without saying
            // which, because they are different promises to a family.
            if (clearance != VerificationClearance.None)
            {
                caregiver.MarkVerified(clearance == VerificationClearance.PeopleCare, utcNow);

                logger.LogInformation(
                    "Caregiver {CaregiverProfileId} is now verified for {Clearance}",
                    caregiver.Id,
                    clearance);
            }
        }

        audit.Record(nameof(VerificationCase), verificationCase.Id, "VerificationApproved", staffId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return verificationCase.ToResponse();
    }
}

public sealed class RejectVerificationHandler(
    IVerificationRepository verifications,
    ICaregiverProfileRepository caregiverProfiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<RejectVerificationCommand, VerificationCaseResponse>
{
    public async Task<VerificationCaseResponse> HandleAsync(
        RejectVerificationCommand command,
        CancellationToken cancellationToken)
    {
        var staffId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var verificationCase = await verifications.FindByIdAsync(command.CaseId, cancellationToken)
            ?? throw new NotFoundException("That verification case does not exist.");

        var utcNow = clock.UtcNow;
        verificationCase.Reject(staffId, command.Reason, utcNow);

        // Rejecting a required check delists the caregiver immediately.
        var caregiver = await caregiverProfiles.FindByIdAsync(
            verificationCase.CaregiverProfileId, cancellationToken);

        if (caregiver is not null
            && VerificationRequirements.AffectsClearance(verificationCase.CheckType))
        {
            // Recomputed without the case just rejected, rather than assumed. Rejecting a
            // vulnerable sector check from someone who also holds a valid criminal record
            // check leaves them cleared for pets; rejecting it from anyone else, or
            // rejecting their photo ID, delists them.
            var remaining = (await verifications.ListForCaregiverAsync(caregiver.Id, cancellationToken))
                .Where(c => c.Id != verificationCase.Id);

            var clearance = VerificationRequirements.ClearanceFrom(remaining, DateOnly.FromDateTime(utcNow));

            if (clearance == VerificationClearance.None)
            {
                caregiver.MarkVerificationRejected(utcNow);
            }
            else if (clearance == VerificationClearance.PetCare && caregiver.ClearedForPeopleCare)
            {
                caregiver.MarkVerified(clearedForPeopleCare: false, utcNow);
            }
        }

        audit.Record(nameof(VerificationCase), verificationCase.Id, "VerificationRejected", staffId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return verificationCase.ToResponse();
    }
}

public sealed class ListPendingVerificationsHandler(
    IVerificationRepository verifications,
    IAdminLookupRepository lookups)
    : IQueryHandler<ListPendingVerificationsQuery, IReadOnlyList<PendingVerification>>
{
    /// <summary>Shown when an account has been deleted but its case rows remain.</summary>
    private static readonly AdminUserSummary Unknown =
        new(Guid.Empty, "Unknown caregiver", string.Empty, "Caregiver", "Unknown");

    public async Task<IReadOnlyList<PendingVerification>> HandleAsync(
        ListPendingVerificationsQuery query,
        CancellationToken cancellationToken)
    {
        var cases = await verifications.ListPendingAsync(
            Math.Clamp(query.Take, 1, 200), cancellationToken);

        // Batched across the whole queue, so the list costs two lookups rather than two
        // per row.
        var owners = await lookups.ListCaregiverOwnersAsync(
            cases.Select(c => c.CaregiverProfileId).Distinct().ToList(), cancellationToken);

        var users = await lookups.ListUsersAsync(
            owners.Values.Distinct().ToList(), cancellationToken);

        return cases
            .Select(c =>
            {
                var caregiver = owners.TryGetValue(c.CaregiverProfileId, out var userId)
                    && users.TryGetValue(userId, out var found)
                        ? found
                        : Unknown;

                return new PendingVerification(
                    c.Id,
                    c.CaregiverProfileId,
                    caregiver.UserId,
                    caregiver.FullName,
                    caregiver.Email,
                    caregiver.Status,
                    c.CheckType.ToString(),
                    c.Status.ToString(),
                    c.SubmittedAtUtc,
                    c.ProviderStatus,
                    c.ProviderScore);
            })
            .ToList();
    }
}

public sealed class ListOpenIncidentsHandler(
    IIncidentRepository incidents,
    IAdminLookupRepository lookups)
    : IQueryHandler<ListOpenIncidentsQuery, IReadOnlyList<AdminIncident>>
{
    private static readonly AdminUserSummary Unknown =
        new(Guid.Empty, "Unknown account", string.Empty, "Unknown", "Unknown");

    public async Task<IReadOnlyList<AdminIncident>> HandleAsync(
        ListOpenIncidentsQuery query,
        CancellationToken cancellationToken)
    {
        var reports = await incidents.ListOpenAsync(
            Math.Clamp(query.Take, 1, 200), cancellationToken);

        var users = await lookups.ListUsersAsync(
            reports
                .SelectMany(r => new[] { r.ReportedByUserId, r.SubjectUserId })
                .Distinct()
                .ToList(),
            cancellationToken);

        return reports
            .Select(r =>
            {
                var reporter = users.GetValueOrDefault(r.ReportedByUserId, Unknown);
                var subject = users.GetValueOrDefault(r.SubjectUserId, Unknown);

                return new AdminIncident(
                    r.Id,
                    r.ReportedByUserId,
                    reporter.FullName,
                    r.SubjectUserId,
                    subject.FullName,
                    subject.Email,
                    subject.Role,
                    subject.Status,
                    r.CareBookingId,
                    r.Category.ToString(),
                    r.Severity.ToString(),
                    r.Status.ToString(),
                    r.RequiresUrgentReview,
                    r.Description,
                    r.AssignedToUserId,
                    r.CreatedAtUtc);
            })
            .ToList();
    }
}

public sealed class ResolveIncidentHandler(
    IIncidentRepository incidents,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<ResolveIncidentCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        ResolveIncidentCommand command,
        CancellationToken cancellationToken)
    {
        var staffId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var report = await incidents.FindByIdAsync(command.ReportId, cancellationToken)
            ?? throw new NotFoundException("That report does not exist.");

        var utcNow = clock.UtcNow;

        if (command.Dismiss)
        {
            report.Dismiss(staffId, command.Notes, utcNow);
        }
        else
        {
            report.Resolve(staffId, command.Notes, utcNow);
        }

        audit.Record(
            nameof(IncidentReport),
            report.Id,
            command.Dismiss ? "IncidentDismissed" : "IncidentResolved",
            staffId);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class SuspendAccountHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<SuspendAccountHandler> logger) : ICommandHandler<SuspendAccountCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        SuspendAccountCommand command,
        CancellationToken cancellationToken)
    {
        var staffId = currentUser.UserId ?? throw new AuthenticationFailedException();

        if (command.UserId == staffId)
        {
            throw new ConflictException("You cannot suspend your own account.");
        }

        var user = await users.FindTrackedByIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("That account does not exist.");

        // Suspending revokes every refresh token, so the account is signed out
        // everywhere rather than staying live until its tokens expire.
        user.Suspend(clock.UtcNow);

        audit.Record(nameof(User), user.Id, "AccountSuspended", staffId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // The reason is recorded in the audit trail, not the log line.
        logger.LogWarning("Account {UserId} suspended by staff {StaffUserId}", user.Id, staffId);

        return Unit.Value;
    }
}

public sealed class ReinstateAccountHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser) : ICommandHandler<ReinstateAccountCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        ReinstateAccountCommand command,
        CancellationToken cancellationToken)
    {
        var staffId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var user = await users.FindTrackedByIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("That account does not exist.");

        user.Reactivate();

        audit.Record(nameof(User), user.Id, "AccountReinstated", staffId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
