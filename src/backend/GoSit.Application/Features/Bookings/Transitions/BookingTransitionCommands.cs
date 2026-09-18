using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Bookings;
using GoSit.Domain.CareServices;

namespace GoSit.Application.Features.Bookings.Transitions;

/// <summary>
/// The caregiver-side lifecycle transitions. Each is its own command so the API surface
/// and the audit trail name the actual business action rather than a status patch.
///
/// Letting a client PATCH a status field would put the state machine in the client's
/// hands; these do the opposite.
/// </summary>
public sealed record AcceptBookingCommand(Guid BookingId) : ICommand<BookingResponse>;

public sealed record DeclineBookingCommand(Guid BookingId, string? Reason) : ICommand<BookingResponse>;

/// <summary>
/// Starting work. Coordinates are optional and are evidence, not a condition — a phone
/// that cannot fix a location must never stop someone starting a shift. See
/// <see cref="Domain.Bookings.ArrivalEvidence"/>.
/// </summary>
public sealed record CheckInCommand(Guid BookingId, DeviceFix? Fix = null)
    : ICommand<BookingResponse>;

public sealed record CheckOutCommand(Guid BookingId, DeviceFix? Fix = null)
    : ICommand<BookingResponse>;

/// <summary>
/// Where the device says it is. Reaches the domain, is turned into a distance, and is
/// never stored.
/// </summary>
public sealed record DeviceFix(double Latitude, double Longitude);

/// <summary>Cancellation is available to both sides; the handler works out which.</summary>
public sealed record CancelBookingCommand(Guid BookingId, string? Reason) : ICommand<BookingResponse>;

public sealed record DisputeBookingCommand(Guid BookingId, string Reason) : ICommand<BookingResponse>;

public sealed class DeclineBookingValidator : AbstractValidator<DeclineBookingCommand>
{
    public DeclineBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class CancelBookingValidator : AbstractValidator<CancelBookingCommand>
{
    public CancelBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class DisputeBookingValidator : AbstractValidator<DisputeBookingCommand>
{
    public DisputeBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>
/// Shared plumbing for the transitions: resolve the caller's profile, load the booking,
/// confirm they are a participant, apply the transition, audit it, and save.
///
/// A booking the caller has nothing to do with is reported as not found rather than
/// forbidden, so booking ids cannot be probed.
/// </summary>
public abstract class BookingTransitionHandler(
    IBookingRepository bookings,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    BookingProjector projector,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
{
    protected IDateTimeProvider Clock { get; } = clock;

    protected IBookingRepository Bookings { get; } = bookings;

    protected async Task<BookingResponse> ApplyAsync(
        Guid bookingId,
        string auditAction,
        Func<CareBooking, BookingActor, DateTime, Task> transition,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var booking = await Bookings.FindByIdAsync(bookingId, cancellationToken)
            ?? throw new NotFoundException("That booking does not exist.");

        var actor = await ResolveActorAsync(userId, booking, cancellationToken);

        await transition(booking, actor, Clock.UtcNow);

        audit.Record(nameof(CareBooking), booking.Id, auditAction, userId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await projector.ProjectAsync(
            booking,
            actor.IsCaregiver ? BookingViewer.Caregiver : BookingViewer.CareRequester,
            cancellationToken);
    }

    /// <summary>
    /// Turns a device fix into evidence, or into nothing when none was offered.
    ///
    /// Done here rather than inside the aggregate so the coordinates are compared once
    /// and only the distance travels on — nothing downstream is ever handed a point it
    /// could accidentally persist or log.
    /// </summary>
    protected static ArrivalEvidence EvidenceFrom(DeviceFix? fix, CareBooking booking) =>
        fix is null
            ? ArrivalEvidence.None
            // Boarding is at the caregiver's own home: comparing where they are with where
            // they live says nothing, and "location confirmed" would read to a family as
            // evidence of something.
            : booking.CareLocation == CareLocation.CaregiverHome
                ? new ArrivalEvidence(ArrivalVerification.NotComparable, null)
                : ArrivalEvidence.From(fix.Latitude, fix.Longitude, booking.Address);

    private async Task<BookingActor> ResolveActorAsync(
        Guid userId,
        CareBooking booking,
        CancellationToken cancellationToken)
    {
        var caregiver = await caregiverProfiles.FindByUserIdAsync(userId, cancellationToken);

        if (caregiver is not null && booking.CaregiverProfileId == caregiver.Id)
        {
            return new BookingActor(caregiver.Id, IsCaregiver: true);
        }

        var requester = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken);

        if (requester is not null && booking.CareRequesterProfileId == requester.Id)
        {
            return new BookingActor(requester.Id, IsCaregiver: false);
        }

        // Not a participant. 404 rather than 403 — a 403 would confirm the id exists.
        throw new NotFoundException("That booking does not exist.");
    }
}

/// <summary>Which side of the booking is acting, and their profile id.</summary>
public sealed record BookingActor(Guid ProfileId, bool IsCaregiver);
