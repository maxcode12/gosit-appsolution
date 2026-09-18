using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;

namespace GoSit.Application.Features.Caregivers.SubmitCertification;

/// <summary>
/// Records a certification the caregiver claims to hold. It is stored as Pending and
/// confers no eligibility until Trust and Safety verifies it — a caregiver can never
/// verify their own credential.
/// </summary>
public sealed record SubmitCertificationCommand(
    string CertificationCode,
    DateOnly IssuedOn,
    DateOnly? ExpiresOn) : ICommand<CaregiverCertificationResponse>;

public sealed class SubmitCertificationValidator : AbstractValidator<SubmitCertificationCommand>
{
    public SubmitCertificationValidator()
    {
        RuleFor(x => x.CertificationCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.IssuedOn).NotEqual(default(DateOnly));
    }
}

public sealed class SubmitCertificationHandler(
    ICaregiverProfileRepository profiles,
    ICertificationRepository certifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<SubmitCertificationCommand, CaregiverCertificationResponse>
{
    public async Task<CaregiverCertificationResponse> HandleAsync(
        SubmitCertificationCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your caregiver profile first.");

        var certification = await certifications.FindByCodeAsync(command.CertificationCode, cancellationToken)
            ?? throw new NotFoundException("That certification type is not recognized.");

        var utcNow = clock.UtcNow;

        var submitted = profile.SubmitCertification(
            certification,
            command.IssuedOn,
            command.ExpiresOn,
            utcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return submitted.ToResponse(DateOnly.FromDateTime(utcNow));
    }
}
