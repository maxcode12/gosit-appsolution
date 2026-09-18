using System.Security.Claims;
using GoSit.Application.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GoSit.Api.Authentication;

/// <summary>Resolves the caller from the validated access token on the current request.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var subject = accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(subject, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
