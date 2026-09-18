using GoSit.Application.Common;
using GoSit.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Middleware;

/// <summary>
/// Converts exceptions into RFC 7807 ProblemDetails responses.
/// Known application errors map to their status code and keep their message, which is
/// written to be safe for clients. Anything else becomes a generic 500 — the real
/// exception goes to structured logs only, never to the caller.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = CreateProblem(exception, httpContext, logger);

        if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static ProblemDetails CreateProblem(
        Exception exception,
        HttpContext httpContext,
        ILogger logger)
    {
        switch (exception)
        {
            case RequestValidationException validation:
                var details = new ValidationProblemDetails(
                    validation.Errors.ToDictionary(e => e.Key, e => e.Value))
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                };
                return details;

            case AuthenticationFailedException:
                // Logged at the handler, where the reason is known. Nothing added here,
                // because the response must not distinguish the causes.
                return Problem(StatusCodes.Status401Unauthorized, exception.Message);

            case ForbiddenException:
                logger.LogWarning("Forbidden request for {Method} {Path}",
                    httpContext.Request.Method, httpContext.Request.Path);
                return Problem(StatusCodes.Status403Forbidden, exception.Message);

            case NotFoundException:
                return Problem(StatusCodes.Status404NotFound, exception.Message);

            case ConflictException:
                return Problem(StatusCodes.Status409Conflict, exception.Message);

            case DomainException:
                logger.LogInformation(exception, "Domain rule violated for {Method} {Path}",
                    httpContext.Request.Method, httpContext.Request.Path);
                return Problem(StatusCodes.Status400BadRequest, exception.Message);

            default:
                logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                    httpContext.Request.Method, httpContext.Request.Path);
                return Problem(
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred.");
        }
    }

    private static ProblemDetails Problem(int status, string title) => new()
    {
        Status = status,
        Title = title,
    };
}
