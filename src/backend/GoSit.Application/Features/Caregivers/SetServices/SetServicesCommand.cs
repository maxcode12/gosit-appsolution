using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;

namespace GoSit.Application.Features.Caregivers.SetServices;

/// <summary>
/// Replaces the set of services a caregiver offers.
///
/// A caregiver may list a service they do not yet hold the certifications for. The
/// claim is allowed; the eligibility is not. Search only surfaces them for services
/// they actually qualify for, and booking re-checks before acceptance.
/// </summary>
public sealed record SetServicesCommand(IReadOnlyList<string> ServiceCodes)
    : ICommand<CaregiverProfileResponse>;

public sealed class SetServicesValidator : AbstractValidator<SetServicesCommand>
{
    public SetServicesValidator()
    {
        RuleFor(x => x.ServiceCodes).NotNull();
        RuleForEach(x => x.ServiceCodes).NotEmpty().MaximumLength(50);
    }
}

public sealed class SetServicesHandler(
    ICaregiverProfileRepository profiles,
    ICaregiverSearchRepository services,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<SetServicesCommand, CaregiverProfileResponse>
{
    public async Task<CaregiverProfileResponse> HandleAsync(
        SetServicesCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your caregiver profile first.");

        var resolved = new List<Domain.CareServices.CareService>();

        foreach (var code in command.ServiceCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            resolved.Add(
                await services.FindServiceByCodeAsync(code, cancellationToken)
                ?? throw new NotFoundException($"The service '{code}' is not recognized."));
        }

        var utcNow = clock.UtcNow;
        profile.ReplaceServices(resolved, utcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return profile.ToOwnerResponse(DateOnly.FromDateTime(utcNow));
    }
}
