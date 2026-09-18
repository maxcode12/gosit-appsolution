using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRequesters;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Users;

namespace GoSit.Application.Features.Caregivers.UpsertProfile;

public sealed record UpsertCaregiverProfileCommand(
    string? Bio,
    int YearsOfExperience,
    decimal HourlyRate,
    AddressDto? Address,
    int ServiceRadiusKm,

    /// <summary>Price per night for boarding and in-home pet sitting. Null for a caregiver who offers neither.</summary>
    decimal? NightlyRate = null) : ICommand<CaregiverProfileResponse>;

public sealed class UpsertCaregiverProfileValidator : AbstractValidator<UpsertCaregiverProfileCommand>
{
    public UpsertCaregiverProfileValidator()
    {
        RuleFor(x => x.Bio).MaximumLength(2000);
        RuleFor(x => x.YearsOfExperience).InclusiveBetween(0, 70);

        // The domain owns the real floor and ceiling; this only rejects nonsense early.
        RuleFor(x => x.HourlyRate).GreaterThan(0);
        RuleFor(x => x.NightlyRate).GreaterThan(0).When(x => x.NightlyRate.HasValue);
        RuleFor(x => x.ServiceRadiusKm).InclusiveBetween(1, 100);

        When(x => x.Address is not null, () =>
        {
            RuleFor(x => x.Address!.Line1).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Address!.City).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Address!.Province).NotEmpty().Length(2);
            RuleFor(x => x.Address!.PostalCode).NotEmpty().MaximumLength(7);
        });
    }
}

public sealed class UpsertCaregiverProfileHandler(
    AddressResolver addressResolver,
    ICaregiverProfileRepository profiles,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<UpsertCaregiverProfileCommand, CaregiverProfileResponse>
{
    public async Task<CaregiverProfileResponse> HandleAsync(
        UpsertCaregiverProfileCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("The account no longer exists.");

        if (user.Role != UserRole.Caregiver)
        {
            throw new ForbiddenException("Only a caregiver can hold a caregiver profile.");
        }

        var utcNow = clock.UtcNow;
        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            profile = CaregiverProfile.Create(
                userId, command.HourlyRate, command.YearsOfExperience, utcNow);
            profiles.Add(profile);
        }
        else
        {
            profile.SetRate(command.HourlyRate, utcNow);
            profile.SetExperience(command.YearsOfExperience, utcNow);
        }

        profile.SetNightlyRate(command.NightlyRate, utcNow);
        profile.UpdateBio(command.Bio, utcNow);

        if (command.Address is { } address)
        {
            // Resolved on the way in, so search can rank by distance immediately. A
            // geocoding failure is non-fatal — see AddressResolver.
            var resolved = await addressResolver.ResolveAsync(address.ToDomain(), cancellationToken);

            profile.SetServiceArea(resolved, command.ServiceRadiusKm, utcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return profile.ToOwnerResponse(DateOnly.FromDateTime(utcNow));
    }
}
