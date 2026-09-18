using GoSit.Application.Abstractions;
using GoSit.Application.Common;

namespace GoSit.Application.Features.Caregivers.SetAcceptingWork;

/// <summary>
/// Turns availability for new work on or off.
///
/// This is the caregiver's own switch — the one that says "I am open for requests" — and
/// it is the last condition in <c>CaregiverProfile.IsListable</c>. Without it a verified
/// caregiver with a complete profile still never appears in search, because the flag
/// starts false and nothing else can set it.
///
/// Turning it on requires verification; the domain enforces that, not this handler.
/// Turning it off is always allowed: someone who needs to stop taking work should never
/// have to satisfy a condition first.
/// </summary>
public sealed record SetAcceptingWorkCommand(bool AcceptingWork) : ICommand<CaregiverProfileResponse>;

public sealed class SetAcceptingWorkHandler(
    ICaregiverProfileRepository profiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<SetAcceptingWorkCommand, CaregiverProfileResponse>
{
    public async Task<CaregiverProfileResponse> HandleAsync(
        SetAcceptingWorkCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("You do not have a caregiver profile yet.");

        var utcNow = clock.UtcNow;

        profile.SetAcceptingWork(command.AcceptingWork, utcNow);

        // Whether someone was open for work at a given moment is a question incident
        // reviews ask, so the change is recorded rather than only reflected in state.
        audit.Record(
            "CaregiverProfile",
            profile.Id,
            command.AcceptingWork ? "StartedAcceptingWork" : "StoppedAcceptingWork",
            userId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return profile.ToOwnerResponse(DateOnly.FromDateTime(utcNow));
    }
}
