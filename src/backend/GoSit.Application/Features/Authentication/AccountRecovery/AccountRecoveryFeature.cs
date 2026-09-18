using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Authentication.AccountRecovery;

/// <summary>
/// Email verification and password reset.
///
/// Three rules run through everything here, and each exists because the obvious
/// implementation gets it wrong:
///
/// 1. **Requesting never reveals whether an account exists.** Both request endpoints
///    return the same response whatever happened. An endpoint that 404s on an unknown
///    address is a free tool for working out who has a GoSit account — which, for a
///    product where the accounts belong to families arranging care, is not a small leak.
/// 2. **Redeeming never says *why* it failed.** Wrong, expired and already-used are one
///    message. Distinguishing them tells an attacker whether a guessed token existed.
/// 3. **Only the hash is stored.** The plaintext lives long enough to build one email.
/// </summary>
public sealed record RequestEmailVerificationCommand : ICommand<Unit>;

public sealed record VerifyEmailCommand(string Token) : ICommand<Unit>;

public sealed record RequestPasswordResetCommand(string Email) : ICommand<Unit>;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand<Unit>;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty();

        // Kept in step with RegisterValidator: a reset must not be a way around the
        // password policy.
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(Register.RegisterValidator.MinimumPasswordLength)
            .WithMessage(
                $"Use at least {Register.RegisterValidator.MinimumPasswordLength} characters.");
    }
}

public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320);
    }
}

/// <summary>How long each kind of link stays good.</summary>
public static class RecoveryTokenLifetimes
{
    /// <summary>
    /// Generous: someone verifying an address may not check that mailbox today, and an
    /// expired verification link is a support ticket rather than a security event.
    /// </summary>
    public static readonly TimeSpan EmailVerification = TimeSpan.FromDays(7);

    /// <summary>
    /// Deliberately short. A reset link is a live key to the account, and the window in
    /// which a forwarded or intercepted email is dangerous should be small.
    /// </summary>
    public static readonly TimeSpan PasswordReset = TimeSpan.FromHours(1);
}

public sealed class RequestEmailVerificationHandler(
    IUserRepository users,
    IAccountRecoveryNotifier notifier,
    ISecureTokenGenerator tokens,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<RequestEmailVerificationCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        RequestEmailVerificationCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var user = await users.FindTrackedByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("The account no longer exists.");

        if (user.EmailVerified)
        {
            // Nothing to do, and no reason to send another email.
            return Unit.Value;
        }

        var (token, hash) = tokens.Generate();

        user.IssueToken(
            UserTokenPurpose.EmailVerification,
            hash,
            clock.UtcNow,
            RecoveryTokenLifetimes.EmailVerification);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifier.SendEmailVerificationAsync(
            user.Email, user.FirstName, token, cancellationToken);

        return Unit.Value;
    }
}

public sealed class VerifyEmailHandler(
    IUserRepository users,
    ISecureTokenGenerator tokens,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock) : ICommandHandler<VerifyEmailCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        VerifyEmailCommand command,
        CancellationToken cancellationToken)
    {
        var invalid = new RequestValidationException(
            new Dictionary<string, string[]>
            {
                ["token"] = ["This link is no longer valid. Ask for a new one."],
            });

        var hash = tokens.Hash(command.Token);

        var user = await users.FindByTokenHashAsync(
            UserTokenPurpose.EmailVerification, hash, cancellationToken)
            ?? throw invalid;

        if (!user.TryConsumeToken(UserTokenPurpose.EmailVerification, hash, clock.UtcNow))
        {
            throw invalid;
        }

        user.VerifyEmail();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class RequestPasswordResetHandler(
    IUserRepository users,
    IAccountRecoveryNotifier notifier,
    ISecureTokenGenerator tokens,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<RequestPasswordResetHandler> logger)
    : ICommandHandler<RequestPasswordResetCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        RequestPasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(command.Email, cancellationToken);

        /*
         * No such account, or one that cannot sign in anyway: return successfully having
         * done nothing.
         *
         * This is the whole enumeration defence. The caller gets the same response as a
         * real request, so the endpoint cannot be used to find out who has an account.
         * A suspended account is included on purpose — resetting the password of an
         * account that has been removed from the platform would be a way back in.
         */
        if (user is null || !user.CanAuthenticate)
        {
            logger.LogInformation(
                "A password reset was requested for an address with no usable account");

            return Unit.Value;
        }

        var (token, hash) = tokens.Generate();

        user.IssueToken(
            UserTokenPurpose.PasswordReset,
            hash,
            clock.UtcNow,
            RecoveryTokenLifetimes.PasswordReset);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifier.SendPasswordResetAsync(
            user.Email, user.FirstName, token, cancellationToken);

        return Unit.Value;
    }
}

public sealed class ResetPasswordHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IAccountRecoveryNotifier notifier,
    ISecureTokenGenerator tokens,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock) : ICommandHandler<ResetPasswordCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var invalid = new RequestValidationException(
            new Dictionary<string, string[]>
            {
                ["token"] = ["This link is no longer valid. Ask for a new one."],
            });

        var hash = tokens.Hash(command.Token);

        var user = await users.FindByTokenHashAsync(
            UserTokenPurpose.PasswordReset, hash, cancellationToken)
            ?? throw invalid;

        var utcNow = clock.UtcNow;

        if (!user.TryConsumeToken(UserTokenPurpose.PasswordReset, hash, utcNow))
        {
            throw invalid;
        }

        // ChangePassword revokes every refresh token: whoever prompted the reset is
        // signed out everywhere, which is the point of resetting.
        user.ChangePassword(passwordHasher.Hash(command.NewPassword), utcNow);

        /*
         * Someone who reaches here has proved control of the mailbox, so the address is
         * verified as a side effect. Making them click a second link to prove the same
         * thing would be theatre.
         */
        user.VerifyEmail();

        /*
         * And lets them back in immediately if a lockout was running.
         *
         * Someone locked out is told nothing about why — saying so would confirm the
         * address exists — which leaves the reset as the only route back they can find
         * unaided. Making them serve out the timer as well would punish the person who
         * did the right thing, and control of the mailbox is stronger evidence than the
         * password that got them locked out.
         */
        user.ClearLockout();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Told after the fact, not asked. If this was not them, this email is how they
        // find out — which is why it is sent even though the change already happened.
        await notifier.SendPasswordChangedAsync(
            user.Email, user.FirstName, cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Sends the account-recovery emails.
///
/// Separate from the notification pipeline on purpose. Those are dispatched out of band
/// on a poll, which is right for "you have a new message" and wrong for a password reset
/// — someone staring at a mailbox waiting for a link should not be waiting on a sweep.
/// These are sent inline, and their contents are never persisted.
/// </summary>
public interface IAccountRecoveryNotifier
{
    Task SendEmailVerificationAsync(
        string email, string firstName, string token, CancellationToken cancellationToken);

    Task SendPasswordResetAsync(
        string email, string firstName, string token, CancellationToken cancellationToken);

    /// <summary>Tells someone their password changed. Their warning if it was not them.</summary>
    Task SendPasswordChangedAsync(
        string email, string firstName, CancellationToken cancellationToken);
}

/// <summary>
/// Makes the tokens that go in those emails.
///
/// An abstraction so a test can make them predictable, and so the one place that decides
/// how much entropy a reset link carries is findable.
/// </summary>
public interface ISecureTokenGenerator
{
    /// <summary>A new token and its hash. The token is returned exactly once.</summary>
    (string Token, string Hash) Generate();

    string Hash(string token);
}
