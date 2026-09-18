using GoSit.Application.Abstractions;

namespace GoSit.Application.Features.Authentication.GetCurrentUser;

public sealed record GetCurrentUserQuery : IQuery<CurrentUserResponse>;

/// <summary>
/// Profile of the authenticated caller. Deliberately narrow — it exposes only what
/// the client needs to render navigation and gate UI.
/// </summary>
public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool EmailVerified);
