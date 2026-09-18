using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.CareRequesters;

namespace GoSit.Application.Features.CareRequesters.EmergencyContacts;

/// <summary>
/// The people a caregiver should reach when the family cannot be.
///
/// `EmergencyContact` has existed since Phase 2, with `AddEmergencyContact` and
/// `RemoveEmergencyContact` on the aggregate and an `EmergencyContacts` array on the
/// profile response — and no endpoint that could put anything in it. The array was
/// permanently empty, so a caregiver alone in a house with a child and an unanswered phone
/// had nobody to call.
///
/// The domain class already said contact details are "released only to a caregiver on a
/// confirmed booking". That rule is implemented in
/// <see cref="Bookings.GetBookingEmergencyContacts.GetBookingEmergencyContactsQuery"/>;
/// this is only the family recording them.
/// </summary>
public sealed record AddEmergencyContactCommand(
    string FullName,
    string Relationship,
    string PhoneNumber) : ICommand<EmergencyContactResponse>;

public sealed record RemoveEmergencyContactCommand(Guid EmergencyContactId) : ICommand<Unit>;

public sealed class AddEmergencyContactValidator : AbstractValidator<AddEmergencyContactCommand>
{
    public AddEmergencyContactValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);

        // "Grandmother", "neighbour", "our GP" — how a caregiver decides which of five
        // people to ring first, and whether this is someone who can collect a child.
        RuleFor(x => x.Relationship).NotEmpty().MaximumLength(100);

        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(30);
    }
}

public sealed class AddEmergencyContactHandler(
    ICareRequesterProfileRepository profiles,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<AddEmergencyContactCommand, EmergencyContactResponse>
{
    public async Task<EmergencyContactResponse> HandleAsync(
        AddEmergencyContactCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your profile before adding contacts.");

        var contact = profile.AddEmergencyContact(
            command.FullName, command.Relationship, command.PhoneNumber, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return contact.ToResponse();
    }
}

public sealed class RemoveEmergencyContactHandler(
    ICareRequesterProfileRepository profiles,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<RemoveEmergencyContactCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        RemoveEmergencyContactCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("That contact does not exist.");

        /*
         * Removal goes through the aggregate, which only knows this family's contacts, so
         * another family's id is "does not exist" rather than a forbidden — the same rule
         * the care-recipient routes follow, and for the same reason: a 403 would confirm
         * the id is real.
         */
        try
        {
            profile.RemoveEmergencyContact(command.EmergencyContactId, clock.UtcNow);
        }
        catch (Domain.Common.DomainException)
        {
            throw new NotFoundException("That contact does not exist.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
