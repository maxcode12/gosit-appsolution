using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Caregivers;

namespace GoSit.Application.Features.Caregivers.SubmitInsurance;

/// <summary>
/// Records an insurance policy the caregiver claims to hold. Stored as Pending and
/// confers no eligibility until Trust and Safety verifies it, mirroring
/// <see cref="Caregivers.SubmitCertification.SubmitCertificationCommand"/> exactly:
/// GoSit verifies a caregiver's own coverage, and never issues or holds it — see
/// docs/BUSINESS_MODEL.md.
/// </summary>
public sealed record SubmitInsuranceCommand(
    InsuranceCoverageType CoverageType,
    string Insurer,
    string PolicyReference,
    decimal LiabilityLimit,
    DateOnly EffectiveOn,
    DateOnly ExpiresOn) : ICommand<CaregiverInsurancePolicyResponse>;

public sealed class SubmitInsuranceValidator : AbstractValidator<SubmitInsuranceCommand>
{
    public SubmitInsuranceValidator()
    {
        RuleFor(x => x.CoverageType).IsInEnum();
        RuleFor(x => x.Insurer).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PolicyReference).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LiabilityLimit).GreaterThan(0);
        RuleFor(x => x.EffectiveOn).NotEqual(default(DateOnly));
        RuleFor(x => x.ExpiresOn).GreaterThan(x => x.EffectiveOn);
    }
}

public sealed class SubmitInsuranceHandler(
    ICaregiverProfileRepository profiles,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<SubmitInsuranceCommand, CaregiverInsurancePolicyResponse>
{
    public async Task<CaregiverInsurancePolicyResponse> HandleAsync(
        SubmitInsuranceCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your caregiver profile first.");

        var utcNow = clock.UtcNow;

        var submitted = profile.SubmitInsurance(
            command.CoverageType,
            command.Insurer,
            command.PolicyReference,
            command.LiabilityLimit,
            command.EffectiveOn,
            command.ExpiresOn,
            utcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return submitted.ToResponse(DateOnly.FromDateTime(utcNow));
    }
}
