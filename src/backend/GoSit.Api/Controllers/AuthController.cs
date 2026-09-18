using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.Authentication;
using GoSit.Application.Features.Authentication.GetCurrentUser;
using GoSit.Application.Features.Authentication.Login;
using GoSit.Application.Features.Authentication.Logout;
using GoSit.Application.Features.Authentication.Refresh;
using GoSit.Application.Features.Authentication.Register;
using GoSit.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GoSit.Api.Controllers;

/// <summary>
/// Authentication endpoints. The controller stays thin: it maps request contracts to
/// commands, validates, delegates to the feature handler, and shapes the response.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting(RateLimitPolicies.Authentication)]
public sealed class AuthController(
    ICommandHandler<RegisterCommand, RegisterResponse> register,
    ICommandHandler<LoginCommand, LoginResponse> login,
    ICommandHandler<RefreshCommand, RefreshResponse> refresh,
    ICommandHandler<LogoutCommand, LogoutResponse> logout) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticationTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthenticationTokens>> Register(
        [FromBody] RegisterRequest request,
        [FromServices] IValidator<RegisterCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            request.Role);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        var result = await register.HandleAsync(command, cancellationToken);

        return Ok(result.Tokens);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticationTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticationTokens>> Login(
        [FromBody] LoginRequest request,
        [FromServices] IValidator<LoginCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        var result = await login.HandleAsync(command, cancellationToken);

        return Ok(result.Tokens);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticationTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticationTokens>> Refresh(
        [FromBody] RefreshRequest request,
        [FromServices] IValidator<RefreshCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new RefreshCommand(request.RefreshToken);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        var result = await refresh.HandleAsync(command, cancellationToken);

        return Ok(result.Tokens);
    }

    /// <summary>
    /// Revokes a refresh token. Anonymous by design so a client whose access token has
    /// already expired can still end its session; the handler checks token ownership.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        [FromServices] IValidator<LogoutCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new LogoutCommand(request.RefreshToken, request.AllSessions);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        await logout.HandleAsync(command, cancellationToken);

        return NoContent();
    }
}

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    UserRole Role);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken, bool AllSessions = false);
