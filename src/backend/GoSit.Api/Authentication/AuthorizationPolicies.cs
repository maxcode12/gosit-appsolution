using GoSit.Domain.Users;
using Microsoft.AspNetCore.Authorization;

namespace GoSit.Api.Authentication;

/// <summary>
/// Named policies used by controllers. Role membership is the coarse gate only —
/// every endpoint that touches a specific resource must also verify ownership in
/// its handler. Role alone is never sufficient authorization in GoSit.
/// </summary>
public static class AuthorizationPolicies
{
    public const string CareRequesterOnly = "CareRequesterOnly";
    public const string CaregiverOnly = "CaregiverOnly";
    public const string AdminOnly = "AdminOnly";

    public static AuthorizationOptions AddGoSitPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(CareRequesterOnly, policy =>
            policy.RequireAuthenticatedUser().RequireRole(nameof(UserRole.CareRequester)));

        options.AddPolicy(CaregiverOnly, policy =>
            policy.RequireAuthenticatedUser().RequireRole(nameof(UserRole.Caregiver)));

        options.AddPolicy(AdminOnly, policy =>
            policy.RequireAuthenticatedUser().RequireRole(nameof(UserRole.Admin)));

        // Anything not explicitly marked [AllowAnonymous] requires a valid token.
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        return options;
    }
}
