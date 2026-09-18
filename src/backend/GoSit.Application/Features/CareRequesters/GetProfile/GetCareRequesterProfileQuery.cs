using GoSit.Application.Abstractions;
using GoSit.Application.Common;

namespace GoSit.Application.Features.CareRequesters.GetProfile;

public sealed record GetCareRequesterProfileQuery : IQuery<CareRequesterProfileResponse>;

public sealed class GetCareRequesterProfileHandler(
    ICareRequesterProfileRepository profiles,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : IQueryHandler<GetCareRequesterProfileQuery, CareRequesterProfileResponse>
{
    public async Task<CareRequesterProfileResponse> HandleAsync(
        GetCareRequesterProfileQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("No care-requester profile has been created yet.");

        return profile.ToResponse(DateOnly.FromDateTime(clock.UtcNow));
    }
}
