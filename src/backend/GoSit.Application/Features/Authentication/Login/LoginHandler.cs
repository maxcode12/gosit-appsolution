using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Authentication.Login;

public sealed class LoginHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IDateTimeProvider clock,
    ILogger<LoginHandler> logger) : ICommandHandler<LoginCommand, LoginResponse>
{
    /// <summary>
    /// A valid hash of a value no one can supply. Verified against when the email is
    /// unknown so that a missing account costs the same time as a wrong password,
    /// which keeps the endpoint from being used to enumerate registered addresses.
    /// </summary>
    private readonly Lazy<string> _decoyHash = new(() => passwordHasher.Hash(Guid.NewGuid().ToString()));

    public async Task<LoginResponse> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var normalizedEmail = User.Normalize(command.Email);
        var user = await users.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (user is null)
        {
            passwordHasher.Verify(command.Password, _decoyHash.Value, out _);
            throw new AuthenticationFailedException();
        }

        var utcNow = clock.UtcNow;

        /*
         * The lockout is checked before the password, and refuses even a correct one.
         *
         * Checking it afterwards would leak: an attacker who guessed right would be told
         * so, and one who guessed wrong could time the difference between a verified hash
         * and a short-circuited refusal. The password is still verified below for the
         * same reason the unknown-email branch verifies a decoy — every path through this
         * handler should cost roughly the same.
         */
        if (user.IsLockedOutAt(utcNow))
        {
            passwordHasher.Verify(command.Password, user.PasswordHash, out _);

            logger.LogWarning("Login refused for locked-out user {UserId}", user.Id);
            throw new AuthenticationFailedException();
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash, out var rehashNeeded))
        {
            user.RecordFailedLogin(utcNow);

            // Saved even though the request fails: without persisting the count there is
            // no lockout, only a number that resets when the request ends.
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Failed login attempt for user {UserId}", user.Id);
            throw new AuthenticationFailedException();
        }

        // Suspended and deactivated accounts are refused even with correct credentials,
        // and are given the same message so the account state is not disclosed.
        if (!user.CanAuthenticate)
        {
            logger.LogWarning("Login refused for {Status} user {UserId}", user.Status, user.Id);
            throw new AuthenticationFailedException();
        }

        if (rehashNeeded)
        {
            // The password is unchanged, so this must not revoke existing sessions.
            user.UpgradePasswordHash(passwordHasher.Hash(command.Password));
            logger.LogInformation("Upgraded password hash parameters for user {UserId}", user.Id);
        }

        user.RecordSuccessfulLogin(utcNow);

        var refreshToken = tokenService.CreateRefreshToken();
        user.IssueRefreshToken(refreshToken.Hash, utcNow, tokenService.RefreshTokenLifetime);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.CreateAccessToken(user);

        return new LoginResponse(new AuthenticationTokens(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refreshToken.Value,
            user.Id,
            user.Role.ToString()));
    }
}
