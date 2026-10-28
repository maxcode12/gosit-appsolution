using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Common;
using GoSit.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Authentication.GoogleSignIn;

/// <summary>
/// Signs in, or signs up, with a Google ID token.
/// </summary>
/// <param name="IdToken">The token Google gave the client.</param>
/// <param name="Role">
/// Needed only when no account exists yet: a new person must say whether they are a
/// family or a caregiver. Ignored for anyone who already has an account.
/// </param>
public sealed record GoogleSignInCommand(string IdToken, UserRole? Role)
    : ICommand<GoogleSignInResponse>;

public sealed record GoogleSignInResponse(AuthenticationTokens Tokens);

public sealed class GoogleSignInValidator : AbstractValidator<GoogleSignInCommand>
{
    public GoogleSignInValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty().MaximumLength(4096);
    }
}

public sealed class GoogleSignInHandler(
    IGoogleIdTokenVerifier verifier,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IRegistrationPolicy registrationPolicy,
    IDateTimeProvider clock,
    ILogger<GoogleSignInHandler> logger) : ICommandHandler<GoogleSignInCommand, GoogleSignInResponse>
{
    public async Task<GoogleSignInResponse> HandleAsync(
        GoogleSignInCommand command,
        CancellationToken cancellationToken)
    {
        var identity = await verifier.VerifyAsync(command.IdToken, cancellationToken);

        // An address Google has not verified proves nothing about who controls it, and
        // matching on it would let anyone claim someone else's GoSit account.
        if (identity is null || !identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Email))
        {
            logger.LogInformation("Google sign-in refused: the token could not be accepted.");
            throw new AuthenticationFailedException();
        }

        var utcNow = clock.UtcNow;

        var user = await users.FindTrackedByGoogleSubjectAsync(identity.Subject, cancellationToken);

        if (user is null)
        {
            var normalizedEmail = User.Normalize(identity.Email);
            user = await users.FindTrackedByNormalizedEmailAsync(normalizedEmail, cancellationToken);

            if (user is null)
            {
                user = CreateAccount(command, identity, normalizedEmail, utcNow);
            }
            else
            {
                // Staff sign in with a password only; an admin account is never one
                // Google email away from being taken.
                if (user.Role == UserRole.Admin)
                {
                    logger.LogWarning("Google sign-in refused for an administrator account {UserId}", user.Id);
                    throw new AuthenticationFailedException();
                }

                try
                {
                    user.LinkGoogle(identity.Subject);
                }
                catch (DomainException)
                {
                    throw new AuthenticationFailedException();
                }

                logger.LogInformation("Google account linked to user {UserId}", user.Id);
            }
        }

        if (!user.CanAuthenticate)
        {
            logger.LogWarning("Google sign-in refused for {Status} user {UserId}", user.Status, user.Id);
            throw new AuthenticationFailedException();
        }

        user.RecordSuccessfulLogin(utcNow);

        var refreshToken = tokenService.CreateRefreshToken();
        user.IssueRefreshToken(refreshToken.Hash, utcNow, tokenService.RefreshTokenLifetime);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.CreateAccessToken(user);

        return new GoogleSignInResponse(new AuthenticationTokens(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refreshToken.Value,
            user.Id,
            user.Role.ToString()));
    }

    private User CreateAccount(
        GoogleSignInCommand command,
        GoogleIdentity identity,
        string normalizedEmail,
        DateTime utcNow)
    {
        if (command.Role is null)
        {
            // Not an error to hide: the client uses this to ask "family or caregiver?".
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["role"] = ["Choose whether you are looking for care or providing it."],
            });
        }

        if (!registrationPolicy.MayRegister(normalizedEmail))
        {
            logger.LogInformation("Google sign-up refused: registration is closed to this address.");
            throw new ForbiddenException(
                "Sign-up is not open yet. Contact us and we will let you know when it is.");
        }

        User user;

        try
        {
            user = User.RegisterWithGoogle(
                identity.Subject,
                identity.Email,
                identity.GivenName ?? string.Empty,
                identity.FamilyName ?? string.Empty,
                command.Role.Value,
                utcNow);
        }
        catch (DomainException ex)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["role"] = [ex.Message],
            });
        }

        users.Add(user);

        // Identifiers only — never the email address or name.
        logger.LogInformation("User {UserId} registered with Google as {Role}", user.Id, user.Role);

        return user;
    }
}
