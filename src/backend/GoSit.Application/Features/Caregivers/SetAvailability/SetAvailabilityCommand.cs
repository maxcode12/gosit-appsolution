using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Caregivers;

namespace GoSit.Application.Features.Caregivers.SetAvailability;

/// <summary>
/// Replaces the caregiver's whole weekly schedule. Replacement rather than per-slot
/// edits keeps the no-overlap rule enforceable against the complete set.
/// </summary>
public sealed record SetAvailabilityCommand(
    IReadOnlyList<AvailabilityWindowDto> Windows) : ICommand<CaregiverProfileResponse>;

public sealed class SetAvailabilityValidator : AbstractValidator<SetAvailabilityCommand>
{
    public SetAvailabilityValidator()
    {
        RuleFor(x => x.Windows).NotNull();

        RuleForEach(x => x.Windows).ChildRules(window =>
        {
            window.RuleFor(w => w.Day).IsInEnum();

            // Not "end after start" any more: an end at or before the start means the
            // window runs past midnight, which is how overnight care is expressed. Equal
            // times stay refused, because they could mean a full day or none at all.
            window.RuleFor(w => w.EndTime)
                .NotEqual(w => w.StartTime)
                .WithMessage(
                    "An availability window must have a start and end that differ. "
                    + "To work past midnight, set an end time earlier in the day than the start.");
        });
    }
}

public sealed class SetAvailabilityHandler(
    ICaregiverProfileRepository profiles,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<SetAvailabilityCommand, CaregiverProfileResponse>
{
    public async Task<CaregiverProfileResponse> HandleAsync(
        SetAvailabilityCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your caregiver profile first.");

        var utcNow = clock.UtcNow;

        profile.ReplaceAvailability(
            command.Windows.Select(w => new AvailabilityWindow(w.Day, w.StartTime, w.EndTime)),
            utcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return profile.ToOwnerResponse(DateOnly.FromDateTime(utcNow));
    }
}
