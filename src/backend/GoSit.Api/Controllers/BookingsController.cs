using FluentValidation;
using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.Bookings;
using GoSit.Application.Features.Bookings.CreateBooking;
using GoSit.Application.Features.Bookings.GetBooking;
using GoSit.Application.Features.Bookings.GetBookingCareNeeds;
using GoSit.Application.Features.Bookings.GetBookingEmergencyContacts;
using GoSit.Application.Features.Bookings.Transitions;
using GoSit.Application.Features.CareRequesters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// Care bookings. Both sides of the marketplace use this controller, so it is not gated
/// by a role policy — each handler resolves which side the caller is on and enforces the
/// rules for that side.
///
/// Every lifecycle change is its own action. There is deliberately no way to PATCH a
/// status: the state machine belongs to the server.
/// </summary>
[ApiController]
[Route("api/v1/bookings")]
[Authorize]
public sealed class BookingsController(
    ICommandHandler<CreateBookingCommand, BookingResponse> create,
    ICommandHandler<AcceptBookingCommand, BookingResponse> accept,
    ICommandHandler<DeclineBookingCommand, BookingResponse> decline,
    ICommandHandler<CancelBookingCommand, BookingResponse> cancel,
    ICommandHandler<CheckInCommand, BookingResponse> checkIn,
    ICommandHandler<CheckOutCommand, BookingResponse> checkOut,
    ICommandHandler<DisputeBookingCommand, BookingResponse> dispute,
    IQueryHandler<GetBookingQuery, BookingResponse> get,
    IQueryHandler<ListBookingsQuery, IReadOnlyList<BookingResponse>> list,
    IQueryHandler<GetBookingCareNeedsQuery, BookingCareNeedsResponse> getCareNeeds,
    IQueryHandler<GetBookingEmergencyContactsQuery, BookingEmergencyContactsResponse> getEmergencyContacts)
    : ControllerBase
{
    /// <summary>Requests care. The price is computed server-side and never sent by the client.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CareRequesterOnly)]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> Create(
        [FromBody] CreateBookingRequest request,
        [FromServices] IValidator<CreateBookingCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new CreateBookingCommand(
            request.CaregiverProfileId,
            request.CareServiceCode,
            request.CareRecipientIds,
            request.StartAtUtc,
            request.EndAtUtc,
            request.Address,
            request.Instructions,
            request.PetIds);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        var created = await create.HandleAsync(command, cancellationToken);

        return CreatedAtAction(nameof(Get), new { bookingId = created.Id }, created);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] bool upcomingOnly,
        CancellationToken cancellationToken) =>
        Ok(await list.HandleAsync(new ListBookingsQuery(status, upcomingOnly), cancellationToken));

    [HttpGet("{bookingId:guid}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> Get(
        Guid bookingId,
        CancellationToken cancellationToken) =>
        Ok(await get.HandleAsync(new GetBookingQuery(bookingId), cancellationToken));

    /// <summary>
    /// The recorded care needs of one recipient on this booking.
    ///
    /// The only route by which a caregiver reads a care-needs profile, and only once
    /// they have accepted the booking. See <see cref="GetBookingCareNeedsQuery"/> for
    /// the full set of conditions; every read is audited.
    /// </summary>
    [HttpGet("{bookingId:guid}/recipients/{careRecipientId:guid}/care-needs")]
    [ProducesResponseType(typeof(BookingCareNeedsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingCareNeedsResponse>> GetCareNeeds(
        Guid bookingId,
        Guid careRecipientId,
        CancellationToken cancellationToken) =>
        Ok(await getCareNeeds.HandleAsync(
            new GetBookingCareNeedsQuery(bookingId, careRecipientId), cancellationToken));

    /// <summary>
    /// Who to ring if the family cannot be reached during this booking.
    ///
    /// The only route by which a caregiver gets a family's emergency contacts, and only
    /// once they have taken the booking on. Same conditions as the care-needs route, and
    /// every read is audited — see <see cref="GetBookingEmergencyContactsQuery"/>.
    /// </summary>
    [HttpGet("{bookingId:guid}/emergency-contacts")]
    [ProducesResponseType(typeof(BookingEmergencyContactsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingEmergencyContactsResponse>> GetEmergencyContacts(
        Guid bookingId,
        CancellationToken cancellationToken) =>
        Ok(await getEmergencyContacts.HandleAsync(
            new GetBookingEmergencyContactsQuery(bookingId), cancellationToken));

    [HttpPost("{bookingId:guid}/accept")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> Accept(
        Guid bookingId,
        CancellationToken cancellationToken) =>
        Ok(await accept.HandleAsync(new AcceptBookingCommand(bookingId), cancellationToken));

    [HttpPost("{bookingId:guid}/decline")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> Decline(
        Guid bookingId,
        [FromBody] ReasonRequest? request,
        [FromServices] IValidator<DeclineBookingCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new DeclineBookingCommand(bookingId, request?.Reason);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await decline.HandleAsync(command, cancellationToken));
    }

    /// <summary>Available to both parties; the server decides which one is cancelling.</summary>
    [HttpPost("{bookingId:guid}/cancel")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> Cancel(
        Guid bookingId,
        [FromBody] ReasonRequest? request,
        [FromServices] IValidator<CancelBookingCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new CancelBookingCommand(bookingId, request?.Reason);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await cancel.HandleAsync(command, cancellationToken));
    }

    [HttpPost("{bookingId:guid}/check-in")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> CheckIn(
        Guid bookingId,
        [FromBody] DeviceFixRequest? request,
        CancellationToken cancellationToken) =>
        Ok(await checkIn.HandleAsync(
            new CheckInCommand(bookingId, request?.ToFix()), cancellationToken));

    /// <summary>Ends the booking and bills the hours actually worked.</summary>
    [HttpPost("{bookingId:guid}/check-out")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> CheckOut(
        Guid bookingId,
        [FromBody] DeviceFixRequest? request,
        CancellationToken cancellationToken) =>
        Ok(await checkOut.HandleAsync(
            new CheckOutCommand(bookingId, request?.ToFix()), cancellationToken));

    [HttpPost("{bookingId:guid}/dispute")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> Dispute(
        Guid bookingId,
        [FromBody] ReasonRequest request,
        [FromServices] IValidator<DisputeBookingCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new DisputeBookingCommand(bookingId, request.Reason ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await dispute.HandleAsync(command, cancellationToken));
    }
}

/// <summary>
/// Note the absence of any price field: totals are derived from the caregiver's stored
/// rate, so a client cannot influence what a booking costs.
/// </summary>
public sealed record CreateBookingRequest(
    Guid CaregiverProfileId,
    string CareServiceCode,
    IReadOnlyList<Guid>? CareRecipientIds,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    AddressDto? Address,
    string? Instructions,

    /// <summary>Pets for a pet-care booking. A booking names people or pets, never both.</summary>
    IReadOnlyList<Guid>? PetIds = null);

public sealed record ReasonRequest(string? Reason);

/// <summary>
/// Where the caregiver's device says it is when starting or finishing.
///
/// The whole body is optional, and so is sending one at all. A phone that cannot fix a
/// location — indoors, in a basement, permission declined, an old handset — still checks
/// in; the booking simply records that no location was offered. Refusing would leave
/// someone standing at the door unable to start work, and would fall hardest on whoever
/// has the worst phone.
///
/// Out of range is treated the same way: obviously wrong coordinates are dropped rather
/// than rejected, because a bad fix is the device's problem and must not become the
/// caregiver's.
/// </summary>
public sealed record DeviceFixRequest(double? Latitude, double? Longitude)
{
    public DeviceFix? ToFix() =>
        Latitude is { } lat && Longitude is { } lon
            && lat is >= -90 and <= 90
            && lon is >= -180 and <= 180
            ? new DeviceFix(lat, lon)
            : null;
}
