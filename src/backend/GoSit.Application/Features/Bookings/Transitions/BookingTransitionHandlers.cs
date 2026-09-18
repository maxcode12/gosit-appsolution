using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Bookings;
using GoSit.Domain.Notifications;
using GoSit.Domain.Payouts;

namespace GoSit.Application.Features.Bookings.Transitions;

public sealed class AcceptBookingHandler(
    IBookingRepository bookings,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    BookingProjector projector,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    BookingNotifier notifier)
    : BookingTransitionHandler(bookings, requesterProfiles, caregiverProfiles, unitOfWork, audit,
        projector, currentUser, clock),
        ICommandHandler<AcceptBookingCommand, BookingResponse>
{
    public Task<BookingResponse> HandleAsync(
        AcceptBookingCommand command,
        CancellationToken cancellationToken) =>
        ApplyAsync(command.BookingId, "BookingAccepted", async (booking, actor, utcNow) =>
        {
            if (!actor.IsCaregiver)
            {
                throw new ForbiddenException("Only the caregiver can accept this booking.");
            }

            // Reloaded rather than reusing the actor lookup, because Accept re-checks
            // eligibility and needs the certifications and availability with it.
            var caregiver = await Bookings.FindCaregiverForBookingAsync(
                booking.CaregiverProfileId, cancellationToken)
                ?? throw new NotFoundException("That caregiver profile no longer exists.");

            var service = await Bookings.FindServiceByIdAsync(booking.CareServiceId, cancellationToken)
                ?? throw new NotFoundException("That care service no longer exists.");

            // A caregiver may have taken another job since this was requested.
            if (await Bookings.HasOverlappingBookingAsync(
                    booking.CaregiverProfileId, booking.StartAtUtc, booking.EndAtUtc,
                    booking.Id, cancellationToken))
            {
                throw new ConflictException("You already have a booking at that time.");
            }

            booking.Accept(caregiver, service, utcNow);

            await notifier.NotifyAsync(
                booking,
                NotificationType.BookingAccepted,
                "Your booking is confirmed",
                "Your caregiver has accepted. Open GoSit for the details.",
                NotifyWho.CareRequester,
                utcNow,
                cancellationToken);
        }, cancellationToken);
}

public sealed class DeclineBookingHandler(
    IBookingRepository bookings,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    BookingProjector projector,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    BookingNotifier notifier)
    : BookingTransitionHandler(bookings, requesterProfiles, caregiverProfiles, unitOfWork, audit,
        projector, currentUser, clock),
        ICommandHandler<DeclineBookingCommand, BookingResponse>
{
    public Task<BookingResponse> HandleAsync(
        DeclineBookingCommand command,
        CancellationToken cancellationToken) =>
        ApplyAsync(command.BookingId, "BookingDeclined", async (booking, actor, utcNow) =>
        {
            if (!actor.IsCaregiver)
            {
                throw new ForbiddenException("Only the caregiver can decline this booking.");
            }

            booking.Decline(actor.ProfileId, command.Reason, utcNow);

            // The reason is deliberately left out: it is the caregiver's words about why
            // they cannot take a job, and it belongs on the booking rather than in an
            // email subject line.
            await notifier.NotifyAsync(
                booking,
                NotificationType.BookingDeclined,
                "Your booking was declined",
                "That caregiver cannot take it. Open GoSit to find someone else.",
                NotifyWho.CareRequester,
                utcNow,
                cancellationToken);
        }, cancellationToken);
}

public sealed class CancelBookingHandler(
    IBookingRepository bookings,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    BookingProjector projector,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    BookingNotifier notifier)
    : BookingTransitionHandler(bookings, requesterProfiles, caregiverProfiles, unitOfWork, audit,
        projector, currentUser, clock),
        ICommandHandler<CancelBookingCommand, BookingResponse>
{
    public Task<BookingResponse> HandleAsync(
        CancelBookingCommand command,
        CancellationToken cancellationToken) =>
        ApplyAsync(command.BookingId, "BookingCancelled", async (booking, actor, utcNow) =>
        {
            // The party is derived from who is calling, never taken from the request —
            // otherwise a requester could book a late-cancellation fee to the caregiver.
            var party = actor.IsCaregiver
                ? CancellationParty.Caregiver
                : CancellationParty.CareRequester;

            booking.Cancel(party, actor.ProfileId, command.Reason, utcNow);

            /*
             * Told to whoever did not cancel — and this is the notification that matters
             * most in the set. A caregiver cancelling the evening before leaves a family
             * without care for the morning, and until now they would have found out by
             * opening the app.
             */
            await notifier.NotifyAsync(
                booking,
                NotificationType.BookingCancelled,
                "A booking was cancelled",
                "Open GoSit to see which one.",
                actor.IsCaregiver ? NotifyWho.CareRequester : NotifyWho.Caregiver,
                utcNow,
                cancellationToken);
        }, cancellationToken);
}

public sealed class CheckInHandler(
    IBookingRepository bookings,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    BookingProjector projector,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : BookingTransitionHandler(bookings, requesterProfiles, caregiverProfiles, unitOfWork, audit,
        projector, currentUser, clock),
        ICommandHandler<CheckInCommand, BookingResponse>
{
    public Task<BookingResponse> HandleAsync(
        CheckInCommand command,
        CancellationToken cancellationToken) =>
        ApplyAsync(command.BookingId, "BookingStarted", (booking, actor, utcNow) =>
        {
            if (!actor.IsCaregiver)
            {
                throw new ForbiddenException("Only the caregiver can check in.");
            }

            booking.CheckIn(actor.ProfileId, utcNow, EvidenceFrom(command.Fix, booking));
            return Task.CompletedTask;
        }, cancellationToken);
}

public sealed class CheckOutHandler(
    IBookingRepository bookings,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    BookingProjector projector,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    BookingCompletionEffects completion,
    BookingNotifier notifier)
    : BookingTransitionHandler(bookings, requesterProfiles, caregiverProfiles, unitOfWork, audit,
        projector, currentUser, clock),
        ICommandHandler<CheckOutCommand, BookingResponse>
{
    public Task<BookingResponse> HandleAsync(
        CheckOutCommand command,
        CancellationToken cancellationToken) =>
        ApplyAsync(command.BookingId, "BookingCompleted", async (booking, actor, utcNow) =>
        {
            if (!actor.IsCaregiver)
            {
                throw new ForbiddenException("Only the caregiver can check out.");
            }

            // Computes the amount actually owed from the hours worked.
            booking.CheckOut(actor.ProfileId, utcNow, EvidenceFrom(command.Fix, booking));

            // Inside the same transaction as the completion, so the two cannot disagree.
            await completion.ApplyAsync(booking, utcNow, cancellationToken);

            await notifier.NotifyAsync(
                booking,
                NotificationType.BookingCompleted,
                "Your booking is finished",
                "Open GoSit to see what was billed and to leave a review.",
                NotifyWho.CareRequester,
                utcNow,
                cancellationToken);
        }, cancellationToken);
}

public sealed class DisputeBookingHandler(
    IBookingRepository bookings,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverProfileRepository caregiverProfiles,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    BookingProjector projector,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IPayoutRepository payouts,
    BookingNotifier notifier)
    : BookingTransitionHandler(bookings, requesterProfiles, caregiverProfiles, unitOfWork, audit,
        projector, currentUser, clock),
        ICommandHandler<DisputeBookingCommand, BookingResponse>
{
    public Task<BookingResponse> HandleAsync(
        DisputeBookingCommand command,
        CancellationToken cancellationToken) =>
        ApplyAsync(command.BookingId, "BookingDisputed", async (booking, actor, utcNow) =>
        {
            // Either party may dispute; the aggregate enforces the window.
            booking.Dispute(actor.ProfileId, command.Reason, utcNow);

            /*
             * And the money stops.
             *
             * Raising a dispute without holding the payout would leave the sweep free to
             * send it minutes later — which is the position the whole dispute window was
             * in until payouts started waiting for it.
             *
             * A payout already in transit cannot be held, and that is not a failure worth
             * refusing the dispute over: the dispute is still recorded, and recovering
             * money that has left is a reversal rather than a hold. Refusing here would
             * mean a family who disputed a minute too late could not raise it at all.
             */
            if (await payouts.FindByBookingIdAsync(booking.Id, cancellationToken) is { } payout
                && payout.Status is PayoutStatus.Queued or PayoutStatus.Failed)
            {
                payout.HoldForDispute();
            }

            /*
             * Both sides, and for the caregiver this is not a courtesy.
             *
             * A dispute stops their payout. Money silently ceasing to arrive, with no
             * message and no date, is the worst version of this — they would assume a bug
             * and contact support, which is the one outcome nobody wants from a dispute.
             */
            await notifier.NotifyAsync(
                booking,
                NotificationType.BookingDisputed,
                "A booking has been disputed",
                "GoSit is looking into it. Open the app to see which booking.",
                NotifyWho.Both,
                utcNow,
                cancellationToken);
        }, cancellationToken);
}
