using FluentValidation;

namespace GoSit.Application.Features.Authentication.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        // Deliberately looser than registration: an existing account may predate any
        // rule change, and a rejected format here would leak nothing useful anyway.
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}
