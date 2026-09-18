using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Common;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Authentication.Refresh;

/// <summary>
/// Exchanges a refresh token for a new token pair, rotating the refresh token.
/// Replaying an already-rotated token revokes every session for that user —
/// the aggregate enforces this; see <see cref="Domain.Users.User.RotateRefreshToken"/>.
/// </summary>
public sealed class RefreshHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IDateTimeProvider clock,
    ILogger<RefreshHandler> logger) : ICommandHandler<RefreshCommand, RefreshResponse>
{
    public async Task<RefreshResponse> HandleAsync(RefreshCommand command, CancellationToken cancellationToken)
    {
        var presentedHash = tokenService.HashRefreshToken(command.RefreshToken);

        var user = await users.FindByRefreshTokenHashAsync(presentedHash, cancellationToken)
            ?? throw new AuthenticationFailedException();

        if (!user.CanAuthenticate)
        {
            throw new AuthenticationFailedException();
        }

        var utcNow = clock.UtcNow;
        var replacement = tokenService.CreateRefreshToken();

        try
        {
            user.RotateRefreshToken(presentedHash, replacement.Hash, utcNow, tokenService.RefreshTokenLifetime);
        }
        catch (DomainException)
        {
            // Reuse of a rotated token: the aggregate has already revoked every session.
            // Persist that revocation before refusing the request.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Refresh token replay detected for user {UserId}; all sessions revoked", user.Id);
            throw new AuthenticationFailedException();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.CreateAccessToken(user);

        return new RefreshResponse(new AuthenticationTokens(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            replacement.Value,
            user.Id,
            user.Role.ToString()));
    }
}
