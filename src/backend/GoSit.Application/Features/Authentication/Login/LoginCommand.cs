using GoSit.Application.Abstractions;

namespace GoSit.Application.Features.Authentication.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResponse>;

public sealed record LoginResponse(AuthenticationTokens Tokens);
