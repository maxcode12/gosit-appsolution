using GoSit.Application.Abstractions;

namespace GoSit.Application.Features.Authentication.Logout;

/// <summary>
/// Revokes the presented refresh token. Set <paramref name="AllSessions"/> to revoke
/// every active token for the caller instead — used for "sign out everywhere".
/// </summary>
public sealed record LogoutCommand(string RefreshToken, bool AllSessions = false) : ICommand<LogoutResponse>;

public sealed record LogoutResponse;
