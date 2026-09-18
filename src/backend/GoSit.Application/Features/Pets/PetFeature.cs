using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Pets;

namespace GoSit.Application.Features.Pets;

/// <summary>
/// A family's pets.
///
/// The family sees everything they recorded — these are their own animals. What a caregiver
/// sees is decided on the booking, where care notes are withheld until the caregiver has
/// accepted.
/// </summary>
public sealed record PetResponse(
    Guid Id,
    string Name,
    string Species,
    string? Breed,
    string? Size,
    string? FeedingInstructions,
    string? Medication,
    string? Behaviour,
    string? VetClinic,
    string? OtherNotes);

/// <summary>The fields shared by adding and editing a pet, so both are validated identically.</summary>
public interface IPetDetails
{
    string Name { get; }

    PetSpecies Species { get; }

    string? Breed { get; }

    PetSize? Size { get; }

    string? FeedingInstructions { get; }

    string? Medication { get; }

    string? Behaviour { get; }

    string? VetClinic { get; }

    string? OtherNotes { get; }
}

public sealed record AddPetCommand(
    string Name,
    PetSpecies Species,
    string? Breed,
    PetSize? Size,
    string? FeedingInstructions,
    string? Medication,
    string? Behaviour,
    string? VetClinic,
    string? OtherNotes) : ICommand<PetResponse>, IPetDetails;

public sealed record UpdatePetCommand(
    Guid PetId,
    string Name,
    PetSpecies Species,
    string? Breed,
    PetSize? Size,
    string? FeedingInstructions,
    string? Medication,
    string? Behaviour,
    string? VetClinic,
    string? OtherNotes) : ICommand<PetResponse>, IPetDetails;

public sealed record RemovePetCommand(Guid PetId) : ICommand<Unit>;

public abstract class PetDetailsValidator<T> : AbstractValidator<T>
    where T : IPetDetails
{
    protected PetDetailsValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Pet.MaxNameLength);
        RuleFor(x => x.Species).IsInEnum();
        RuleFor(x => x.Size).IsInEnum();
        RuleFor(x => x.Breed).MaximumLength(Pet.MaxBreedLength);
        RuleFor(x => x.FeedingInstructions).MaximumLength(Pet.MaxNoteLength);
        RuleFor(x => x.Medication).MaximumLength(Pet.MaxNoteLength);
        RuleFor(x => x.Behaviour).MaximumLength(Pet.MaxNoteLength);
        RuleFor(x => x.VetClinic).MaximumLength(Pet.MaxNoteLength);
        RuleFor(x => x.OtherNotes).MaximumLength(Pet.MaxNoteLength);
    }
}

public sealed class AddPetValidator : PetDetailsValidator<AddPetCommand>
{
}

public sealed class UpdatePetValidator : PetDetailsValidator<UpdatePetCommand>
{
    public UpdatePetValidator()
    {
        RuleFor(x => x.PetId).NotEmpty();
    }
}

public sealed class AddPetHandler(
    ICareRequesterProfileRepository profiles,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<AddPetCommand, PetResponse>
{
    public async Task<PetResponse> HandleAsync(AddPetCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your care-requester profile before adding a pet.");

        var pet = profile.AddPet(command.ToDetails(), clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return pet.ToResponse();
    }
}

public sealed class UpdatePetHandler(
    ICareRequesterProfileRepository profiles,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<UpdatePetCommand, PetResponse>
{
    public async Task<PetResponse> HandleAsync(UpdatePetCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("That pet does not exist.");

        // Someone else's pet is not found rather than forbidden, so ids cannot be probed.
        var pet = profile.FindPet(command.PetId)
            ?? throw new NotFoundException("That pet does not exist.");

        profile.UpdatePet(pet.Id, command.ToDetails(), clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return pet.ToResponse();
    }
}

public sealed class RemovePetHandler(
    ICareRequesterProfileRepository profiles,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<RemovePetCommand, Unit>
{
    public async Task<Unit> HandleAsync(RemovePetCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("That pet does not exist.");

        _ = profile.FindPet(command.PetId)
            ?? throw new NotFoundException("That pet does not exist.");

        // Past bookings keep their own copy of the pet's name and species, so removing it
        // here changes nothing about what those bookings say.
        profile.RemovePet(command.PetId, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public static class PetMapping
{
    public static PetResponse ToResponse(this Pet pet) =>
        new(
            pet.Id,
            pet.Name,
            pet.Species.ToString(),
            pet.Breed,
            pet.Size?.ToString(),
            pet.FeedingInstructions,
            pet.Medication,
            pet.Behaviour,
            pet.VetClinic,
            pet.OtherNotes);

    public static PetDetails ToDetails(this IPetDetails details) =>
        new(
            details.Name,
            details.Species,
            details.Breed,
            details.Size,
            details.FeedingInstructions,
            details.Medication,
            details.Behaviour,
            details.VetClinic,
            details.OtherNotes);
}
