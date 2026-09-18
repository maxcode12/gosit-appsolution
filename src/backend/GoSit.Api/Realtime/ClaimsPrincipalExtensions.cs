using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GoSit.Api.Realtime;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The account id from the validated access token. Matches how HttpContextCurrentUser
    /// resolves it, so HTTP and SignalR agree on who the caller is.
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(subject, out var id) ? id : null;
    }
}
