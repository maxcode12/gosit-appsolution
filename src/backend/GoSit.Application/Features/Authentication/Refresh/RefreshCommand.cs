using GoSit.Application.Abstractions;

namespace GoSit.Application.Features.Authentication.Refresh;

public sealed record RefreshCommand(string RefreshToken) : ICommand<RefreshResponse>;

public sealed record RefreshResponse(AuthenticationTokens Tokens);
