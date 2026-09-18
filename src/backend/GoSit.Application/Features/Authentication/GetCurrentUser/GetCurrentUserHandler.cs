using GoSit.Application.Abstractions;
using GoSit.Application.Common;

namespace GoSit.Application.Features.Authentication.GetCurrentUser;

public sealed class GetCurrentUserHandler(
    IUserRepository users,
    ICurrentUser currentUser) : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    public async Task<CurrentUserResponse> HandleAsync(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            throw new AuthenticationFailedException();
        }

        // A token can outlive the account it names, so the user may legitimately be gone.
        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("The account no longer exists.");

        return new CurrentUserResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role.ToString(),
            user.EmailVerified);
    }
}
