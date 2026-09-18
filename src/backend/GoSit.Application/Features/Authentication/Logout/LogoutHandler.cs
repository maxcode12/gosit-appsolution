using GoSit.Application.Abstractions;
using GoSit.Application.Common;

namespace GoSit.Application.Features.Authentication.Logout;

public sealed class LogoutHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<LogoutCommand, LogoutResponse>
{
    public async Task<LogoutResponse> HandleAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        var presentedHash = tokenService.HashRefreshToken(command.RefreshToken);
        var user = await users.FindByRefreshTokenHashAsync(presentedHash, cancellationToken);

        // Logging out an unknown or already-revoked token succeeds silently: the client's
        // desired end state (no valid session) already holds, and reporting otherwise
        // would turn this endpoint into a token oracle.
        if (user is null)
        {
            return new LogoutResponse();
        }

        // The token must belong to the caller. Without this a leaked token value could be
        // used to sign another user out.
        if (currentUser.UserId is { } callerId && callerId != user.Id)
        {
            throw new ForbiddenException("The refresh token does not belong to the authenticated user.");
        }

        var utcNow = clock.UtcNow;

        if (command.AllSessions)
        {
            user.RevokeAllRefreshTokens(utcNow);
        }
        else
        {
            user.RevokeRefreshToken(presentedHash, utcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new LogoutResponse();
    }
}
