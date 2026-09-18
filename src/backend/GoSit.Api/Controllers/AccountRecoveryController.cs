using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.Authentication.AccountRecovery;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GoSit.Api.Controllers;

/// <summary>
/// Email verification and password reset.
///
/// Under the authentication rate limit, which matters more here than almost anywhere
/// else: these endpoints send email to an address the caller chooses, so without a limit
/// they are a free way to send mail from GoSit's reputation to anyone.
///
/// Every endpoint returns 204 on success and says nothing about whether an account
/// exists — see <see cref="RequestPasswordResetHandler"/> for why.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting(RateLimitPolicies.Authentication)]
public sealed class AccountRecoveryController(
    ICommandHandler<RequestEmailVerificationCommand, Unit> requestVerification,
    ICommandHandler<VerifyEmailCommand, Unit> verifyEmail,
    ICommandHandler<RequestPasswordResetCommand, Unit> requestReset,
    ICommandHandler<ResetPasswordCommand, Unit> resetPassword) : ControllerBase
{
    /// <summary>
    /// Sends a fresh verification link to the signed-in account's own address.
    ///
    /// Authenticated, and takes no address: someone can only ever verify the mailbox
    /// already on their account. An endpoint that accepted an address would be a way to
    /// send GoSit-branded mail to a stranger.
    /// </summary>
    [HttpPost("email-verification")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RequestEmailVerification(CancellationToken cancellationToken)
    {
        await requestVerification.HandleAsync(
            new RequestEmailVerificationCommand(), cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Redeems a verification link. Anonymous — someone clicking a link from their
    /// mailbox may well not be signed in, and requiring a login first would be a strange
    /// way to ask them to prove they own the mailbox.
    /// </summary>
    [HttpPost("email-verification/confirm")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] TokenRequest request,
        CancellationToken cancellationToken)
    {
        await verifyEmail.HandleAsync(
            new VerifyEmailCommand(request.Token ?? string.Empty), cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Starts a password reset.
    ///
    /// Always 204, whether or not the address has an account. That is deliberate and is
    /// the reason this cannot be used to discover who has a GoSit account.
    /// </summary>
    [HttpPost("password-reset")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestPasswordReset(
        [FromBody] PasswordResetRequest request,
        [FromServices] IValidator<RequestPasswordResetCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new RequestPasswordResetCommand(request.Email ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        await requestReset.HandleAsync(command, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Sets a new password against a reset token. Signs the account out everywhere.
    /// </summary>
    [HttpPost("password-reset/confirm")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmPasswordReset(
        [FromBody] ConfirmPasswordResetRequest request,
        [FromServices] IValidator<ResetPasswordCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new ResetPasswordCommand(
            request.Token ?? string.Empty, request.NewPassword ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        await resetPassword.HandleAsync(command, cancellationToken);

        return NoContent();
    }
}

public sealed record TokenRequest(string? Token);

public sealed record PasswordResetRequest(string? Email);

public sealed record ConfirmPasswordResetRequest(string? Token, string? NewPassword);
