using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Caregivers;

namespace GoSit.Application.Features.Administration.InsuranceReview;

/// <summary>
/// Staff review of submitted caregiver insurance policies. Mirrors
/// <see cref="Administration.CertificationReview.ApproveCertificationHandler"/> exactly:
/// a caregiver can submit a policy, but until staff verify it, it confers no eligibility.
/// GoSit verifies coverage; it never issues or holds it — see docs/BUSINESS_MODEL.md.
/// </summary>
public sealed record ListPendingInsuranceQuery(int Take = 50) : IQuery<IReadOnlyList<PendingInsurance>>;

public sealed record PendingInsurance(
    Guid CaregiverInsurancePolicyId,
    Guid CaregiverProfileId,
    Guid CaregiverUserId,
    string CaregiverName,
    string CaregiverEmail,
    string CoverageType,
    string Insurer,
    string PolicyReference,
    decimal LiabilityLimit,
    DateOnly EffectiveOn,
    DateOnly ExpiresOn,

    /// <summary>True when the policy has already lapsed — approving it changes nothing.</summary>
    bool IsExpired,

    bool HasDocument,
    DateTime SubmittedAtUtc);

public sealed record ApproveInsuranceCommand(Guid CaregiverInsurancePolicyId) : ICommand<Unit>;

public sealed record RejectInsuranceCommand(Guid CaregiverInsurancePolicyId, string Reason) : ICommand<Unit>;

public sealed class RejectInsuranceValidator : AbstractValidator<RejectInsuranceCommand>
{
    public RejectInsuranceValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class ListPendingInsuranceHandler(
    IInsuranceReviewRepository insurance,
    IAdminLookupRepository lookups,
    IDateTimeProvider clock) : IQueryHandler<ListPendingInsuranceQuery, IReadOnlyList<PendingInsurance>>
{
    private static readonly AdminUserSummary Unknown =
        new(Guid.Empty, "Unknown caregiver", string.Empty, "Caregiver", "Unknown");

    public async Task<IReadOnlyList<PendingInsurance>> HandleAsync(
        ListPendingInsuranceQuery query,
        CancellationToken cancellationToken)
    {
        var pending = await insurance.ListPendingAsync(Math.Clamp(query.Take, 1, 200), cancellationToken);

        var owners = await lookups.ListCaregiverOwnersAsync(
            pending.Select(p => p.CaregiverProfileId).Distinct().ToList(), cancellationToken);

        var users = await lookups.ListUsersAsync(owners.Values.Distinct().ToList(), cancellationToken);

        var today = DateOnly.FromDateTime(clock.UtcNow);

        return pending
            .Select(p =>
            {
                var caregiver = owners.TryGetValue(p.CaregiverProfileId, out var userId)
                    && users.TryGetValue(userId, out var found)
                        ? found
                        : Unknown;

                return new PendingInsurance(
                    p.CaregiverInsurancePolicyId,
                    p.CaregiverProfileId,
                    caregiver.UserId,
                    caregiver.FullName,
                    caregiver.Email,
                    p.CoverageType.ToString(),
                    p.Insurer,
                    p.PolicyReference,
                    p.LiabilityLimit,
                    p.EffectiveOn,
                    p.ExpiresOn,
                    p.ExpiresOn < today,
                    p.HasDocument,
                    p.SubmittedAtUtc);
            })
            .ToList();
    }
}

public sealed class ApproveInsuranceHandler(
    ICaregiverProfileRepository profiles,
    IInsuranceReviewRepository insurance,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<ApproveInsuranceCommand, Unit>
{
    public async Task<Unit> HandleAsync(ApproveInsuranceCommand command, CancellationToken cancellationToken)
    {
        var profile = await LoadOwnerAsync(profiles, insurance, command.CaregiverInsurancePolicyId, cancellationToken);

        var utcNow = clock.UtcNow;

        profile.ApproveInsurance(command.CaregiverInsurancePolicyId, utcNow);

        audit.Record(
            nameof(CaregiverInsurancePolicy), command.CaregiverInsurancePolicyId, "Approved", currentUser.UserId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }

    internal static async Task<CaregiverProfile> LoadOwnerAsync(
        ICaregiverProfileRepository profiles,
        IInsuranceReviewRepository insurance,
        Guid caregiverInsurancePolicyId,
        CancellationToken cancellationToken)
    {
        var profileId = await insurance.FindOwningProfileIdAsync(caregiverInsurancePolicyId, cancellationToken)
            ?? throw new NotFoundException("That insurance policy does not exist.");

        return await profiles.FindByIdAsync(profileId, cancellationToken)
            ?? throw new NotFoundException("That insurance policy does not exist.");
    }
}

public sealed class RejectInsuranceHandler(
    ICaregiverProfileRepository profiles,
    IInsuranceReviewRepository insurance,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<RejectInsuranceCommand, Unit>
{
    public async Task<Unit> HandleAsync(RejectInsuranceCommand command, CancellationToken cancellationToken)
    {
        var profile = await ApproveInsuranceHandler.LoadOwnerAsync(
            profiles, insurance, command.CaregiverInsurancePolicyId, cancellationToken);

        var utcNow = clock.UtcNow;

        profile.RejectInsurance(command.CaregiverInsurancePolicyId, command.Reason, utcNow);

        audit.Record(
            nameof(CaregiverInsurancePolicy), command.CaregiverInsurancePolicyId, "Rejected", currentUser.UserId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
