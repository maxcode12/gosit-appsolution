using GoSit.Application.Abstractions;
using GoSit.Application.Common;

namespace GoSit.Application.Features.Caregivers.SetAvailableForLastMinuteRequests;

/// <summary>
/// The caregiver's own "I can take something on short notice" switch.
///
/// Separate from <see cref="Caregivers.SetAcceptingWork.SetAcceptingWorkCommand"/>: that
/// one says whether a caregiver appears in search at all, this one says whether they want
/// the modest ranking nudge <c>CaregiverRanking</c> gives someone open to last-minute
/// asks. Turning it on requires verification, mirroring accepting work, for the same
/// reason — an unverified caregiver must not be more findable on any axis.
/// </summary>
public sealed record SetAvailableForLastMinuteRequestsCommand(bool AvailableForLastMinuteRequests)
    : ICommand<CaregiverProfileResponse>;

public sealed class SetAvailableForLastMinuteRequestsHandler(
    ICaregiverProfileRepository profiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<SetAvailableForLastMinuteRequestsCommand, CaregiverProfileResponse>
{
    public async Task<CaregiverProfileResponse> HandleAsync(
        SetAvailableForLastMinuteRequestsCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("You do not have a caregiver profile yet.");

        var utcNow = clock.UtcNow;

        profile.SetAvailableForLastMinuteRequests(command.AvailableForLastMinuteRequests, utcNow);

        audit.Record(
            "CaregiverProfile",
            profile.Id,
            command.AvailableForLastMinuteRequests
                ? "StartedOfferingLastMinuteAvailability"
                : "StoppedOfferingLastMinuteAvailability",
            userId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return profile.ToOwnerResponse(DateOnly.FromDateTime(utcNow));
    }
}
