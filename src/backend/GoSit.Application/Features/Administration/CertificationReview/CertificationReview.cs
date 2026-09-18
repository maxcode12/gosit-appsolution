using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Caregivers;

namespace GoSit.Application.Features.Administration.CertificationReview;

/// <summary>
/// Staff review of submitted certifications.
///
/// A caregiver can submit a First Aid or CPR certificate, but until someone verifies it
/// the certificate confers no eligibility — <c>IsValidOn</c> requires a verified status.
/// The domain has had approve and reject all along; what was missing was any route to
/// reach them, which meant a submitted certificate could only be cleared by editing the
/// database. That made every service with a certification requirement unbookable in
/// practice.
/// </summary>
public sealed record ListPendingCertificationsQuery(int Take = 50)
    : IQuery<IReadOnlyList<PendingCertification>>;

public sealed record PendingCertification(
    Guid CaregiverCertificationId,
    Guid CaregiverProfileId,
    Guid CaregiverUserId,
    string CaregiverName,
    string CaregiverEmail,
    string Code,
    string Name,
    DateOnly IssuedOn,
    DateOnly? ExpiresOn,
    /// <summary>True when the certificate has already lapsed — approving it changes nothing.</summary>
    bool IsExpired,

    /// <summary>
    /// Whether there is a document to read. The key itself is deliberately absent: it is a
    /// Blob Storage path to a criminal record check, and the queue is a list on a screen.
    /// Staff fetch the file through an endpoint that authorises the request instead.
    /// </summary>
    bool HasDocument,

    DateTime SubmittedAtUtc);

public sealed record ApproveCertificationCommand(Guid CaregiverCertificationId)
    : ICommand<Unit>;

public sealed record RejectCertificationCommand(Guid CaregiverCertificationId, string Reason)
    : ICommand<Unit>;

public sealed class RejectCertificationValidator : AbstractValidator<RejectCertificationCommand>
{
    public RejectCertificationValidator()
    {
        // A caregiver who is refused must be told why, or they cannot fix it.
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class ListPendingCertificationsHandler(
    ICertificationReviewRepository certifications,
    IAdminLookupRepository lookups,
    IDateTimeProvider clock)
    : IQueryHandler<ListPendingCertificationsQuery, IReadOnlyList<PendingCertification>>
{
    private static readonly AdminUserSummary Unknown =
        new(Guid.Empty, "Unknown caregiver", string.Empty, "Caregiver", "Unknown");

    public async Task<IReadOnlyList<PendingCertification>> HandleAsync(
        ListPendingCertificationsQuery query,
        CancellationToken cancellationToken)
    {
        var pending = await certifications.ListPendingAsync(
            Math.Clamp(query.Take, 1, 200), cancellationToken);

        var owners = await lookups.ListCaregiverOwnersAsync(
            pending.Select(c => c.CaregiverProfileId).Distinct().ToList(), cancellationToken);

        var users = await lookups.ListUsersAsync(
            owners.Values.Distinct().ToList(), cancellationToken);

        var today = DateOnly.FromDateTime(clock.UtcNow);

        return pending
            .Select(c =>
            {
                var caregiver = owners.TryGetValue(c.CaregiverProfileId, out var userId)
                    && users.TryGetValue(userId, out var found)
                        ? found
                        : Unknown;

                return new PendingCertification(
                    c.CaregiverCertificationId,
                    c.CaregiverProfileId,
                    caregiver.UserId,
                    caregiver.FullName,
                    caregiver.Email,
                    c.Code,
                    c.Name,
                    c.IssuedOn,
                    c.ExpiresOn,
                    c.ExpiresOn is { } expires && expires < today,
                    c.HasDocument,
                    c.SubmittedAtUtc);
            })
            .ToList();
    }
}

public sealed class ApproveCertificationHandler(
    ICaregiverProfileRepository profiles,
    ICertificationReviewRepository certifications,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<ApproveCertificationCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        ApproveCertificationCommand command,
        CancellationToken cancellationToken)
    {
        var profile = await LoadOwnerAsync(
            profiles, certifications, command.CaregiverCertificationId, cancellationToken);

        var utcNow = clock.UtcNow;

        profile.ApproveCertification(command.CaregiverCertificationId, utcNow);

        audit.Record(
            "CaregiverCertification",
            command.CaregiverCertificationId,
            "Approved",
            currentUser.UserId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }

    internal static async Task<CaregiverProfile> LoadOwnerAsync(
        ICaregiverProfileRepository profiles,
        ICertificationReviewRepository certifications,
        Guid caregiverCertificationId,
        CancellationToken cancellationToken)
    {
        var profileId = await certifications.FindOwningProfileIdAsync(
            caregiverCertificationId, cancellationToken)
            ?? throw new NotFoundException("That certification does not exist.");

        return await profiles.FindByIdAsync(profileId, cancellationToken)
            ?? throw new NotFoundException("That certification does not exist.");
    }
}

public sealed class RejectCertificationHandler(
    ICaregiverProfileRepository profiles,
    ICertificationReviewRepository certifications,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<RejectCertificationCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        RejectCertificationCommand command,
        CancellationToken cancellationToken)
    {
        var profile = await ApproveCertificationHandler.LoadOwnerAsync(
            profiles, certifications, command.CaregiverCertificationId, cancellationToken);

        var utcNow = clock.UtcNow;

        profile.RejectCertification(command.CaregiverCertificationId, command.Reason, utcNow);

        audit.Record(
            "CaregiverCertification",
            command.CaregiverCertificationId,
            "Rejected",
            currentUser.UserId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
