using FluentValidation;
using GoSit.Domain.Users;

namespace GoSit.Application.Features.Authentication.Register;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    /// <summary>Minimum length for a new password. Matches the client-side rule.</summary>
    public const int MinimumPasswordLength = 12;

    public RegisterValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(MinimumPasswordLength)
            .WithMessage($"Password must be at least {MinimumPasswordLength} characters.")
            .MaximumLength(256);

        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);

        // Admin accounts are provisioned internally and must not be self-registered.
        RuleFor(x => x.Role)
            .Must(role => role is UserRole.CareRequester or UserRole.Caregiver)
            .WithMessage("Account type must be either CareRequester or Caregiver.");
    }
}
