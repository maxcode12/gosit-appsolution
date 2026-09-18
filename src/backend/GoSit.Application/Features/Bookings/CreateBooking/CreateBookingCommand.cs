using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRequesters;
using GoSit.Domain.Bookings;
using GoSit.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Bookings.CreateBooking;

/// <summary>
/// Requests care from a chosen caregiver, creating the booking in Pending.
///
/// Note what the client does NOT send: no price. The rate is read from the caregiver's
/// profile and the total is computed server-side, so a manipulated request cannot buy
/// care cheaply.
/// </summary>
public sealed record CreateBookingCommand(
    Guid CaregiverProfileId,
    string CareServiceCode,
    IReadOnlyList<Guid>? CareRecipientIds,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    AddressDto? Address,
    string? Instructions,
    IReadOnlyList<Guid>? PetIds = null) : ICommand<BookingResponse>;

public sealed class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.CaregiverProfileId).NotEmpty();
        RuleFor(x => x.CareServiceCode).NotEmpty().MaximumLength(50);

        // A booking is for people or for pets; the domain decides which the service needs.
        RuleFor(x => x.CareRecipientIds)
            .Must((command, ids) => (ids?.Count ?? 0) > 0 || (command.PetIds?.Count ?? 0) > 0)
            .WithMessage("Name who, or which pets, the care is for.");

        RuleForEach(x => x.CareRecipientIds).NotEmpty();
        RuleForEach(x => x.PetIds).NotEmpty();

        RuleFor(x => x.Instructions).MaximumLength(2000);

        // The domain owns the real scheduling rules; this only rejects nonsense early.
        RuleFor(x => x.EndAtUtc)
            .GreaterThan(x => x.StartAtUtc)
            .WithMessage("A booking must end after it starts.");
    }
}

public sealed class CreateBookingHandler(
    IBookingRepository bookings,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverSearchRepository services,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    BookingProjector projector,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    BookingNotifier notifier,
    ILogger<CreateBookingHandler> logger) : ICommandHandler<CreateBookingCommand, BookingResponse>
{
    public async Task<BookingResponse> HandleAsync(
        CreateBookingCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var requester = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your care-requester profile before booking.");

        var service = await services.FindServiceByCodeAsync(command.CareServiceCode, cancellationToken)
            ?? throw new NotFoundException("That care service is not recognized.");

        var caregiver = await bookings.FindCaregiverForBookingAsync(
            command.CaregiverProfileId, cancellationToken)
            ?? throw new NotFoundException("That caregiver is not available.");

        // Resolved from the requester's own profile, so an id belonging to someone else
        // simply is not found — the aggregate then rejects it as well.
        var recipients = (command.CareRecipientIds ?? Array.Empty<Guid>())
            .Select(id => requester.FindCareRecipient(id)
                ?? throw new NotFoundException("That care recipient does not exist."))
            .ToList();

        var pets = (command.PetIds ?? Array.Empty<Guid>())
            .Select(id => requester.FindPet(id)
                ?? throw new NotFoundException("That pet does not exist."))
            .ToList();

        // Boarding happens at the caregiver's home; the aggregate uses their address whatever
        // is passed, so a family does not need one of their own to board a pet. Everything
        // else defaults to the requester's home, which is the common case.
        var address = service.CareLocation == Domain.CareServices.CareLocation.CaregiverHome
            ? caregiver.Address
                ?? throw new ConflictException(
                    "This caregiver has no home address on file, so they cannot take boarding.")
            : command.Address?.ToDomain()
                ?? requester.Address
                ?? throw new ConflictException(
                    "Add an address to your profile, or supply one for this booking.");

        var utcNow = clock.UtcNow;

        // Double-booking is a scheduling conflict rather than a domain invariant: the
        // aggregate cannot see the caregiver's other bookings.
        if (await bookings.HasOverlappingBookingAsync(
                caregiver.Id, command.StartAtUtc, command.EndAtUtc, null, cancellationToken))
        {
            throw new ConflictException("That caregiver already has a booking at that time.");
        }

        var booking = CareBooking.Request(
            requester.Id,
            caregiver,
            service,
            recipients,
            command.StartAtUtc,
            command.EndAtUtc,
            address,
            command.Instructions,
            utcNow,
            pets);

        bookings.Add(booking);
        audit.Record(nameof(CareBooking), booking.Id, "BookingRequested", userId);

        // The caregiver, not the family — they are the one who has to answer, and a
        // request nobody sees is a request that lapses.
        await notifier.NotifyAsync(
            booking,
            NotificationType.BookingRequested,
            "New booking request",
            "A family has asked you to work. Open GoSit to accept or decline.",
            NotifyWho.Caregiver,
            utcNow,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Identifiers only — never the address, instructions, or recipient details.
        logger.LogInformation(
            "Booking {BookingId} requested from caregiver {CaregiverProfileId} for service {ServiceCode}",
            booking.Id, caregiver.Id, service.Code);

        return await projector.ProjectAsync(booking, BookingViewer.CareRequester, cancellationToken);
    }
}
