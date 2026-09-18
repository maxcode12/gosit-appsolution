using GoSit.Application.Abstractions;
using GoSit.Application.Common;

namespace GoSit.Application.Features.Caregivers.GetProfile;

public sealed record GetCaregiverProfileQuery : IQuery<CaregiverProfileResponse>;

public sealed class GetCaregiverProfileHandler(
    ICaregiverProfileRepository profiles,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : IQueryHandler<GetCaregiverProfileQuery, CaregiverProfileResponse>
{
    public async Task<CaregiverProfileResponse> HandleAsync(
        GetCaregiverProfileQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("No caregiver profile has been created yet.");

        return profile.ToOwnerResponse(DateOnly.FromDateTime(clock.UtcNow));
    }
}
