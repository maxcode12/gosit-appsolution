using GoSit.Domain.Bookings;
using GoSit.Domain.Bookings.Events;
using GoSit.Domain.CareRecipients;
using GoSit.Domain.CareRequesters;
using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Common;
using GoSit.Domain.Common.ValueObjects;
using Xunit;

namespace GoSit.UnitTests.Domain;

public class CareBookingTests
{
    // A Wednesday. Local Toronto time is UTC-4 in August, so 14:00Z is 10:00 local.
    private static readonly DateTime Now = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = new(2026, 8, 19, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 8, 19, 22, 0, 0, DateTimeKind.Utc);

    private static readonly Guid CategoryId = Guid.NewGuid();

    private static CareService Babysitting() => CareService.Create(
        CategoryId, "BABYSITTING", "Babysitting", null,
        ["VULNERABLE_SECTOR"], [CareRecipientType.Child]);

    private static CareService SeniorCompanionship() => CareService.Create(
        CategoryId, "SENIOR_COMPANIONSHIP", "Senior Companionship", null,
        [], [CareRecipientType.Senior]);

    private static Address SomeAddress() =>
        Address.Create("12 Bloor St W", null, "Toronto", "ON", "M5S 1M2");

    /// <summary>A caregiver who is verified, listed, qualified and available all day.</summary>
    private static CaregiverProfile BookableCaregiver(CareService? service = null)
    {
        var profile = CaregiverProfile.Create(Guid.NewGuid(), 30m, 5, Now);
        profile.SetServiceArea(SomeAddress(), 25, Now);

        var certification = Certification.Create("VULNERABLE_SECTOR", "Vulnerable Sector Check", true);
        var submitted = profile.SubmitCertification(
            certification, new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1), Now);
        // Approval now requires evidence — see CaregiverCertification.Approve.
        profile.AttachCertificationDocument(submitted.Id, "test/document", Now);
        profile.ApproveCertification(submitted.Id, Now);

        profile.ReplaceServices([service ?? Babysitting()], Now);
        profile.ReplaceAvailability(
            [new AvailabilityWindow(DayOfWeek.Wednesday, new TimeOnly(6, 0), new TimeOnly(23, 0))], Now);

        profile.MarkVerified(clearedForPeopleCare: true, Now);
        profile.AcceptServiceAgreement("Test Caregiver", Now);
        profile.SetAcceptingWork(true, Now);
        return profile;
    }

    private static (CareRequesterProfile Profile, CareRecipient Child) RequesterWithChild()
    {
        var profile = CareRequesterProfile.Create(Guid.NewGuid(), "416-555-0142", Now);
        var child = profile.AddCareRecipient(
            "Sam", null, new DateOnly(2018, 4, 2), CareRecipientType.Child, Now);
        return (profile, child);
    }

    private static CareBooking Book(
        CaregiverProfile? caregiver = null,
        CareService? service = null,
        DateTime? start = null,
        DateTime? end = null)
    {
        var (requester, child) = RequesterWithChild();
        var resolvedService = service ?? Babysitting();

        return CareBooking.Request(
            requester.Id,
            caregiver ?? BookableCaregiver(resolvedService),
            resolvedService,
            [child],
            start ?? Start,
            end ?? End,
            SomeAddress(),
            "Bedtime is 8pm.",
            Now);
    }

    [Fact]
    public void A_new_booking_is_pending_and_priced_from_the_caregivers_rate()
    {
        var booking = Book();

        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(30m, booking.HourlyRateSnapshot);
        Assert.Equal(120m, booking.EstimatedTotal);
        Assert.Null(booking.ActualTotal);
        Assert.Single(booking.Recipients);
    }

    [Fact]
    public void Requesting_raises_a_booking_requested_event()
    {
        var booking = Book();

        var raised = Assert.Single(booking.DomainEvents);
        Assert.IsType<BookingRequestedDomainEvent>(raised);
    }

    /// <summary>
    /// The agreed price must not move when the caregiver later edits their profile.
    /// </summary>
    [Fact]
    public void Raising_the_caregivers_rate_does_not_change_an_existing_booking()
    {
        // One service instance: eligibility matches on id, and each call to
        // Babysitting() builds a separate one.
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);

        caregiver.SetRate(90m, Now);

        Assert.Equal(30m, booking.HourlyRateSnapshot);
        Assert.Equal(120m, booking.EstimatedTotal);
    }

    [Fact]
    public void A_caregiver_who_is_not_listable_cannot_be_booked()
    {
        var caregiver = CaregiverProfile.Create(Guid.NewGuid(), 30m, 5, Now);

        Assert.Throws<DomainException>(() => Book(caregiver));
    }

    [Fact]
    public void A_caregiver_missing_a_required_certification_cannot_be_booked()
    {
        var service = Babysitting();
        var caregiver = CaregiverProfile.Create(Guid.NewGuid(), 30m, 5, Now);
        caregiver.SetServiceArea(SomeAddress(), 25, Now);
        caregiver.ReplaceServices([service], Now);
        caregiver.ReplaceAvailability(
            [new AvailabilityWindow(DayOfWeek.Wednesday, new TimeOnly(6, 0), new TimeOnly(23, 0))], Now);
        caregiver.MarkVerified(clearedForPeopleCare: true, Now);
        caregiver.AcceptServiceAgreement("Test Caregiver", Now);
        caregiver.SetAcceptingWork(true, Now);

        // Listed and available, but never certified.
        Assert.Throws<DomainException>(() => Book(caregiver, service));
    }

    [Fact]
    public void A_caregiver_who_is_not_available_at_that_time_cannot_be_booked()
    {
        var caregiver = BookableCaregiver();
        caregiver.ReplaceAvailability(
            [new AvailabilityWindow(DayOfWeek.Wednesday, new TimeOnly(6, 0), new TimeOnly(12, 0))], Now);

        // 18:00-22:00 UTC is 14:00-18:00 local, past the caregiver's noon finish.
        Assert.Throws<DomainException>(() => Book(caregiver));
    }

    [Fact]
    public void A_service_that_does_not_apply_to_the_recipient_is_rejected()
    {
        var seniorCare = SeniorCompanionship();
        var caregiver = BookableCaregiver(seniorCare);

        // The recipient is a child; senior companionship does not apply to them.
        Assert.Throws<DomainException>(() => Book(caregiver, seniorCare));
    }

    /// <summary>
    /// A requester must not be able to name someone else's care recipient on a booking.
    /// </summary>
    [Fact]
    public void A_recipient_belonging_to_another_requester_is_rejected()
    {
        var (_, theirChild) = RequesterWithChild();
        var service = Babysitting();

        Assert.Throws<DomainException>(() => CareBooking.Request(
            Guid.NewGuid(),
            BookableCaregiver(service),
            service,
            [theirChild],
            Start,
            End,
            SomeAddress(),
            null,
            Now));
    }

    [Fact]
    public void A_booking_must_name_at_least_one_recipient()
    {
        var (requester, _) = RequesterWithChild();
        var service = Babysitting();

        Assert.Throws<DomainException>(() => CareBooking.Request(
            requester.Id, BookableCaregiver(service), service, [],
            Start, End, SomeAddress(), null, Now));
    }

    [Theory]
    // Less than the two hours' notice a caregiver needs to answer.
    [InlineData(1, 4)]
    // Shorter than the one-hour minimum.
    [InlineData(6, 0)]
    // Longer than the fourteen-hour maximum.
    [InlineData(6, 20)]
    public void An_invalid_schedule_is_rejected(int hoursFromNow, int durationHours)
    {
        var start = Now.AddHours(hoursFromNow);
        var end = start.AddHours(durationHours);

        Assert.Throws<DomainException>(() => Book(start: start, end: end));
    }

    [Fact]
    public void Times_must_be_supplied_in_utc()
    {
        var start = DateTime.SpecifyKind(Start, DateTimeKind.Local);

        Assert.Throws<DomainException>(() => Book(start: start, end: End));
    }

    [Fact]
    public void The_assigned_caregiver_can_accept()
    {
        var caregiver = BookableCaregiver();
        var service = Babysitting();
        caregiver.ReplaceServices([service], Now);
        var booking = Book(caregiver, service);

        booking.Accept(caregiver, service, Now);

        Assert.Equal(BookingStatus.Accepted, booking.Status);
        Assert.Equal(Now, booking.AcceptedAtUtc);
    }

    [Fact]
    public void Another_caregiver_cannot_accept_a_booking()
    {
        var service = Babysitting();
        var booking = Book(service: service);
        var stranger = BookableCaregiver(service);

        Assert.Throws<DomainException>(() => booking.Accept(stranger, service, Now));
    }

    /// <summary>
    /// A certification can lapse between request and acceptance, so eligibility is
    /// checked again rather than trusted from request time.
    /// </summary>
    [Fact]
    public void Acceptance_is_refused_when_the_caregiver_is_no_longer_qualified()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);

        caregiver.MarkVerificationRejected(Now);

        Assert.Throws<DomainException>(() => booking.Accept(caregiver, service, Now));
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public void A_booking_cannot_be_accepted_after_it_was_due_to_start()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);

        Assert.Throws<DomainException>(() => booking.Accept(caregiver, service, End));
    }

    [Fact]
    public void A_booking_cannot_be_accepted_twice()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);
        booking.Accept(caregiver, service, Now);

        Assert.Throws<DomainException>(() => booking.Accept(caregiver, service, Now));
    }

    [Fact]
    public void A_pending_booking_can_be_declined()
    {
        var booking = Book();

        booking.Decline(booking.CaregiverProfileId, "Already booked that evening.", Now);

        Assert.Equal(BookingStatus.Declined, booking.Status);
        Assert.Equal("Already booked that evening.", booking.ResolutionReason);
    }

    [Fact]
    public void An_accepted_booking_cannot_be_declined()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);
        booking.Accept(caregiver, service, Now);

        Assert.Throws<DomainException>(() => booking.Decline(caregiver.Id, null, Now));
    }

    [Fact]
    public void Either_party_can_cancel_and_the_canceller_is_recorded()
    {
        var requesterCancelled = Book();
        requesterCancelled.Cancel(
            CancellationParty.CareRequester, requesterCancelled.CareRequesterProfileId, "Plans changed.", Now);

        var caregiverCancelled = Book();
        caregiverCancelled.Cancel(
            CancellationParty.Caregiver, caregiverCancelled.CaregiverProfileId, "Unwell.", Now);

        Assert.Equal(CancellationParty.CareRequester, requesterCancelled.CancelledBy);
        Assert.Equal(CancellationParty.Caregiver, caregiverCancelled.CancelledBy);
    }

    /// <summary>
    /// A participant must not be able to cancel while claiming to be the other side,
    /// which would misattribute a late-cancellation fee.
    /// </summary>
    [Fact]
    public void A_requester_cannot_cancel_as_the_caregiver()
    {
        var booking = Book();

        Assert.Throws<DomainException>(() => booking.Cancel(
            CancellationParty.Caregiver, booking.CareRequesterProfileId, null, Now));
    }

    [Fact]
    public void Cancelling_inside_the_late_window_is_flagged_but_allowed()
    {
        var booking = Book();

        // Six hours before the start, well inside the 24-hour window.
        booking.Cancel(CancellationParty.CareRequester, booking.CareRequesterProfileId, null, Now);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.True(booking.IsLateCancellation);
    }

    [Fact]
    public void Cancelling_well_ahead_is_not_late()
    {
        // A week out lands on the same weekday, so it still falls inside the
        // caregiver's Wednesday availability.
        var start = Start.AddDays(7);
        var booking = Book(start: start, end: start.AddHours(4));

        booking.Cancel(CancellationParty.CareRequester, booking.CareRequesterProfileId, null, Now);

        Assert.False(booking.IsLateCancellation);
    }

    [Fact]
    public void A_completed_booking_cannot_be_cancelled()
    {
        var booking = CompletedBooking(out _);

        Assert.Throws<DomainException>(() => booking.Cancel(
            CancellationParty.CareRequester, booking.CareRequesterProfileId, null, Now));
    }

    [Fact]
    public void Check_in_moves_an_accepted_booking_to_in_progress()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);
        booking.Accept(caregiver, service, Now);

        booking.CheckIn(caregiver.Id, Start);

        Assert.Equal(BookingStatus.InProgress, booking.Status);
        Assert.Equal(Start, booking.CheckedInAtUtc);
    }

    [Fact]
    public void A_caregiver_cannot_check_in_long_before_the_start()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);
        booking.Accept(caregiver, service, Now);

        Assert.Throws<DomainException>(() => booking.CheckIn(caregiver.Id, Start.AddHours(-3)));
    }

    [Fact]
    public void A_caregiver_may_check_in_slightly_early()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);
        booking.Accept(caregiver, service, Now);

        booking.CheckIn(caregiver.Id, Start.AddMinutes(-15));

        Assert.Equal(BookingStatus.InProgress, booking.Status);
    }

    [Fact]
    public void A_pending_booking_cannot_be_checked_in()
    {
        var booking = Book();

        Assert.Throws<DomainException>(() => booking.CheckIn(booking.CaregiverProfileId, Start));
    }

    [Fact]
    public void Check_out_completes_the_booking_and_bills_the_hours_worked()
    {
        var booking = CompletedBooking(out var caregiver);

        Assert.Equal(BookingStatus.Completed, booking.Status);
        Assert.NotNull(booking.CheckedOutAtUtc);
        // Four hours at 30/hour.
        Assert.Equal(120m, booking.ActualTotal);
        Assert.Equal(caregiver.Id, booking.CaregiverProfileId);
    }

    /// <summary>
    /// The requester pays for time actually worked, not the time originally scheduled.
    /// </summary>
    [Fact]
    public void Leaving_early_bills_less_than_the_estimate()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);
        booking.Accept(caregiver, service, Now);
        booking.CheckIn(caregiver.Id, Start);

        booking.CheckOut(caregiver.Id, Start.AddHours(2));

        Assert.Equal(120m, booking.EstimatedTotal);
        Assert.Equal(60m, booking.ActualTotal);
    }

    [Fact]
    public void Billing_rounds_up_to_the_next_quarter_hour()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);
        booking.Accept(caregiver, service, Now);
        booking.CheckIn(caregiver.Id, Start);

        // 61 minutes bills as 1.25 hours.
        booking.CheckOut(caregiver.Id, Start.AddMinutes(61));

        Assert.Equal(37.50m, booking.ActualTotal);
    }

    [Fact]
    public void A_booking_cannot_be_checked_out_before_check_in()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);
        booking.Accept(caregiver, service, Now);

        Assert.Throws<DomainException>(() => booking.CheckOut(caregiver.Id, End));
    }

    [Fact]
    public void Check_out_must_be_after_check_in()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);
        booking.Accept(caregiver, service, Now);
        booking.CheckIn(caregiver.Id, Start);

        Assert.Throws<DomainException>(() => booking.CheckOut(caregiver.Id, Start.AddMinutes(-5)));
    }

    [Fact]
    public void An_overrunning_booking_cannot_be_closed_by_the_caregiver_alone()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);
        booking.Accept(caregiver, service, Now);
        booking.CheckIn(caregiver.Id, Start);

        // Past the fourteen-hour ceiling: needs support rather than a silent huge bill.
        Assert.Throws<DomainException>(() => booking.CheckOut(caregiver.Id, Start.AddHours(20)));
    }

    [Fact]
    public void A_completed_booking_can_be_disputed_by_either_party()
    {
        var booking = CompletedBooking(out _);

        booking.Dispute(booking.CareRequesterProfileId, "The caregiver left early.", Now.AddDays(1));

        Assert.Equal(BookingStatus.Disputed, booking.Status);
    }

    [Fact]
    public void Someone_outside_the_booking_cannot_dispute_it()
    {
        var booking = CompletedBooking(out _);

        Assert.Throws<DomainException>(() =>
            booking.Dispute(Guid.NewGuid(), "Not mine.", Now.AddDays(1)));
    }

    [Fact]
    public void A_dispute_raised_after_the_window_is_refused()
    {
        var booking = CompletedBooking(out _);

        Assert.Throws<DomainException>(() => booking.Dispute(
            booking.CareRequesterProfileId, "Too late.", End.AddDays(30)));
    }

    [Fact]
    public void A_pending_booking_cannot_be_disputed()
    {
        var booking = Book();

        Assert.Throws<DomainException>(() =>
            booking.Dispute(booking.CareRequesterProfileId, "Nothing happened yet.", Now));
    }

    [Fact]
    public void Every_transition_advances_the_concurrency_version()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);

        var atRequest = booking.Version;
        booking.Accept(caregiver, service, Now);
        var afterAccept = booking.Version;
        booking.CheckIn(caregiver.Id, Start);

        Assert.True(afterAccept > atRequest);
        Assert.True(booking.Version > afterAccept);
    }

    [Fact]
    public void Only_participants_are_recognised()
    {
        var booking = Book();

        Assert.True(booking.IsParticipant(booking.CareRequesterProfileId));
        Assert.True(booking.IsParticipant(booking.CaregiverProfileId));
        Assert.False(booking.IsParticipant(Guid.NewGuid()));
    }

    [Fact]
    public void Finished_bookings_are_recognised_as_finished()
    {
        Assert.False(Book().IsFinished);
        Assert.True(CompletedBooking(out _).IsFinished);
    }

    // --- Bookings the lifecycle would otherwise strand -------------------------------
    //
    // Two cases no participant can resolve on their own: a caregiver who checked in and
    // never checked out (past the maximum duration, CheckOut refuses), and a request
    // nobody answered before its start time. Both are closed by the platform sweep.

    [Fact]
    public void Auto_complete_closes_a_booking_the_caregiver_never_checked_out_of()
    {
        var booking = InProgressBooking(out _);

        booking.AutoComplete(Start.AddHours(20));

        Assert.Equal(BookingStatus.Completed, booking.Status);
        Assert.True(booking.WasAutoCompleted);
        Assert.Equal(Start.AddHours(20), booking.CompletedAtUtc);
        // Nobody checked out, so no check-out time is invented.
        Assert.Null(booking.CheckedOutAtUtc);
        Assert.True(booking.IsFinished);
    }

    /// <summary>
    /// The requester must not pay for the hours between the caregiver leaving and the
    /// sweep noticing. Billing follows the schedule both sides agreed to.
    /// </summary>
    [Fact]
    public void Auto_complete_bills_the_scheduled_end_not_the_time_since_check_in()
    {
        var booking = InProgressBooking(out _);

        booking.AutoComplete(Start.AddHours(30));

        // Four scheduled hours at 30/hour — not the thirty hours since check-in.
        Assert.Equal(120m, booking.ActualTotal);
    }

    [Fact]
    public void Auto_complete_of_a_late_check_in_bills_only_to_the_scheduled_end()
    {
        var booking = InProgressBooking(out _, checkInAt: Start.AddHours(1));

        booking.AutoComplete(Start.AddHours(20));

        // Checked in an hour late, so three of the four scheduled hours remained.
        Assert.Equal(90m, booking.ActualTotal);
    }

    /// <summary>
    /// A caregiver who checked in after the booking was already due to end leaves no
    /// window that can honestly be billed.
    /// </summary>
    [Fact]
    public void Auto_complete_bills_nothing_when_check_in_was_after_the_scheduled_end()
    {
        var booking = InProgressBooking(out _, checkInAt: End.AddHours(1));

        booking.AutoComplete(End.AddHours(16));

        Assert.Equal(0m, booking.ActualTotal);
        Assert.Equal(BookingStatus.Completed, booking.Status);
    }

    [Fact]
    public void Auto_complete_raises_a_booking_completed_event()
    {
        var booking = InProgressBooking(out _);
        booking.ClearDomainEvents();

        booking.AutoComplete(Start.AddHours(20));

        var raised = Assert.Single(booking.DomainEvents);
        var completed = Assert.IsType<BookingCompletedDomainEvent>(raised);
        Assert.Equal(booking.Id, completed.BookingId);
        Assert.Equal(120m, completed.Total);
    }

    /// <summary>
    /// While the caregiver can still close the booking themselves, it is theirs to close.
    /// The sweep only takes over once <see cref="CareBooking.CheckOut"/> would refuse.
    /// </summary>
    [Fact]
    public void A_booking_the_caregiver_can_still_check_out_of_is_not_auto_completed()
    {
        var booking = InProgressBooking(out _);

        Assert.Throws<DomainException>(() => booking.AutoComplete(Start.AddHours(10)));
        Assert.Equal(BookingStatus.InProgress, booking.Status);
    }

    [Fact]
    public void Auto_complete_is_refused_at_exactly_the_maximum_duration()
    {
        var booking = InProgressBooking(out _);

        // The boundary belongs to the caregiver: check-out is still allowed here.
        Assert.Throws<DomainException>(() =>
            booking.AutoComplete(Start + BookingPolicy.MaximumDuration));
    }

    [Fact]
    public void A_pending_booking_cannot_be_auto_completed()
    {
        var booking = Book();

        Assert.Throws<DomainException>(() => booking.AutoComplete(End.AddDays(1)));
    }

    [Fact]
    public void An_accepted_booking_that_was_never_started_cannot_be_auto_completed()
    {
        var service = Babysitting();
        var caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);
        booking.Accept(caregiver, service, Now);

        Assert.Throws<DomainException>(() => booking.AutoComplete(End.AddDays(1)));
    }

    [Fact]
    public void An_already_completed_booking_cannot_be_auto_completed_again()
    {
        var booking = CompletedBooking(out _);

        Assert.Throws<DomainException>(() => booking.AutoComplete(End.AddDays(1)));
        Assert.False(booking.WasAutoCompleted);
    }

    [Fact]
    public void Auto_complete_advances_the_concurrency_version()
    {
        var booking = InProgressBooking(out _);
        var before = booking.Version;

        booking.AutoComplete(Start.AddHours(20));

        Assert.True(booking.Version > before);
    }

    /// <summary>
    /// How the sweep closes a request nobody answered: the ordinary cancel path,
    /// attributed to the platform rather than to either participant.
    /// </summary>
    [Fact]
    public void The_platform_can_cancel_a_request_nobody_answered()
    {
        var booking = Book();

        booking.Cancel(CancellationParty.Platform, Guid.Empty, "Nobody answered.", Start.AddHours(1));

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(CancellationParty.Platform, booking.CancelledBy);
        Assert.Equal(Start.AddHours(1), booking.CancelledAtUtc);
    }

    /// <summary>
    /// The caregiver never checked out, so nothing about the booking is confirmed —
    /// the usual dispute window has to stay open on it.
    /// </summary>
    [Fact]
    public void An_auto_completed_booking_can_still_be_disputed()
    {
        var booking = InProgressBooking(out _);
        booking.AutoComplete(Start.AddHours(20));

        booking.Dispute(booking.CareRequesterProfileId, "I was billed for a visit that ended early.",
            Start.AddHours(21));

        Assert.Equal(BookingStatus.Disputed, booking.Status);
    }

    /// <summary>A booking checked into but not yet checked out of.</summary>
    private static CareBooking InProgressBooking(
        out CaregiverProfile caregiver,
        DateTime? checkInAt = null)
    {
        var service = Babysitting();
        caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);
        booking.Accept(caregiver, service, Now);
        booking.CheckIn(caregiver.Id, checkInAt ?? Start);
        return booking;
    }

    private static CareBooking CompletedBooking(out CaregiverProfile caregiver)
    {
        var service = Babysitting();
        caregiver = BookableCaregiver(service);
        var booking = Book(caregiver, service);
        booking.Accept(caregiver, service, Now);
        booking.CheckIn(caregiver.Id, Start);
        booking.CheckOut(caregiver.Id, End);
        return booking;
    }
}
