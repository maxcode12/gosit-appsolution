using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.CareRequesters;
using GoSit.Domain.Users;

namespace GoSit.Application.Features.CareRequesters.UpsertProfile;

/// <summary>
/// Creates the caller's care-requester profile, or updates it if it already exists.
/// Upsert rather than separate create/update: the client does not need to know which
/// it is, and a first-time save and a later edit carry the same payload.
/// </summary>
public sealed record UpsertCareRequesterProfileCommand(
    string PhoneNumber,
    string? AboutMe,
    AddressDto? Address) : ICommand<CareRequesterProfileResponse>;

public sealed class UpsertCareRequesterProfileValidator : AbstractValidator<UpsertCareRequesterProfileCommand>
{
    public UpsertCareRequesterProfileValidator()
    {
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.AboutMe).MaximumLength(2000);

        When(x => x.Address is not null, () =>
        {
            RuleFor(x => x.Address!.Line1).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Address!.Line2).MaximumLength(200);
            RuleFor(x => x.Address!.City).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Address!.Province).NotEmpty().Length(2);
            RuleFor(x => x.Address!.PostalCode).NotEmpty().MaximumLength(7);
        });
    }
}

public sealed class UpsertCareRequesterProfileHandler(
    AddressResolver addressResolver,
    ICareRequesterProfileRepository profiles,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<UpsertCareRequesterProfileCommand, CareRequesterProfileResponse>
{
    public async Task<CareRequesterProfileResponse> HandleAsync(
        UpsertCareRequesterProfileCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        // The role claim on the token is the coarse gate; this confirms against stored
        // state, so a token issued before a role change cannot be used to create the
        // wrong kind of profile.
        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("The account no longer exists.");

        if (user.Role != UserRole.CareRequester)
        {
            throw new ForbiddenException("Only a care requester can hold a care-requester profile.");
        }

        var utcNow = clock.UtcNow;
        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            profile = CareRequesterProfile.Create(userId, command.PhoneNumber, utcNow);
            profiles.Add(profile);
        }

        profile.UpdateContactDetails(command.PhoneNumber, command.AboutMe, utcNow);

        if (command.Address is { } address)
        {
            // Resolved on the way in; a failure is non-fatal. See AddressResolver.
            var resolved = await addressResolver.ResolveAsync(address.ToDomain(), cancellationToken);

            profile.SetAddress(resolved, utcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return profile.ToResponse(DateOnly.FromDateTime(utcNow));
    }
}
