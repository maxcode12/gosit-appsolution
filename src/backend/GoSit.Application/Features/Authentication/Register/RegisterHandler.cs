using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Authentication.Register;

public sealed class RegisterHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IRegistrationPolicy registrationPolicy,
    IDateTimeProvider clock,
    ILogger<RegisterHandler> logger) : ICommandHandler<RegisterCommand, RegisterResponse>
{
    public async Task<RegisterResponse> HandleAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        var normalizedEmail = User.Normalize(command.Email);

        // Before the duplicate check, so a closed door answers the same way for everyone
        // and cannot be used to learn which addresses already hold an account.
        if (!registrationPolicy.MayRegister(normalizedEmail))
        {
            logger.LogInformation("Sign-up refused: registration is closed to this address.");
            throw new ForbiddenException(
                "Sign-up is not open yet. Contact us and we will let you know when it is.");
        }

        if (await users.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException("An account with that email address already exists.");
        }

        var utcNow = clock.UtcNow;

        var user = User.Register(
            command.Email,
            passwordHasher.Hash(command.Password),
            command.FirstName,
            command.LastName,
            command.Role,
            utcNow);

        var refreshToken = tokenService.CreateRefreshToken();
        user.IssueRefreshToken(refreshToken.Hash, utcNow, tokenService.RefreshTokenLifetime);

        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Identifiers only — never the email address or name.
        logger.LogInformation("User {UserId} registered as {Role}", user.Id, user.Role);

        var accessToken = tokenService.CreateAccessToken(user);

        return new RegisterResponse(new AuthenticationTokens(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refreshToken.Value,
            user.Id,
            user.Role.ToString()));
    }
}
