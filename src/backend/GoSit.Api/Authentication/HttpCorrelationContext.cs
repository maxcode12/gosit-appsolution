using GoSit.Api.Middleware;
using GoSit.Application.Abstractions;

namespace GoSit.Api.Authentication;

/// <summary>
/// Exposes the request's correlation id to the application layer, so an audit row can
/// be tied back to the request trace that produced it.
/// </summary>
public sealed class HttpCorrelationContext(IHttpContextAccessor accessor) : ICorrelationContext
{
    public string? CorrelationId =>
        accessor.HttpContext?.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value) == true
            ? value as string
            : null;
}
