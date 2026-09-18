using GoSit.Application.Abstractions;
using GoSit.Domain.Users;

namespace GoSit.Application.Features.Authentication.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    UserRole Role) : ICommand<RegisterResponse>;
