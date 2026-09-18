using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Caregivers;

namespace GoSit.Application.Features.Caregivers.AcceptServiceAgreement;

/// <summary>
/// Electronically signs the Caregiver Service Agreement — the current version, always;
/// see <see cref="ServiceAgreement.CurrentVersion"/>. A caregiver cannot be listed
/// without this: <see cref="CaregiverProfile.IsListable"/> requires it.
/// </summary>
public sealed record AcceptServiceAgreementCommand(string SignedName) : ICommand<CaregiverProfileResponse>;

public sealed class AcceptServiceAgreementValidator : AbstractValidator<AcceptServiceAgreementCommand>
{
    public AcceptServiceAgreementValidator()
    {
        RuleFor(x => x.SignedName).NotEmpty().MaximumLength(200);
    }
}

public sealed class AcceptServiceAgreementHandler(
    ICaregiverProfileRepository profiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<AcceptServiceAgreementCommand, CaregiverProfileResponse>
{
    public async Task<CaregiverProfileResponse> HandleAsync(
        AcceptServiceAgreementCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your caregiver profile first.");

        var utcNow = clock.UtcNow;

        profile.AcceptServiceAgreement(command.SignedName, utcNow);

        // Names the version, never the typed name — a signature is exactly the kind of
        // personal detail an audit log should point at, not repeat.
        audit.Record(nameof(CaregiverProfile), profile.Id, "ServiceAgreementSigned", userId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return profile.ToOwnerResponse(DateOnly.FromDateTime(utcNow));
    }
}
