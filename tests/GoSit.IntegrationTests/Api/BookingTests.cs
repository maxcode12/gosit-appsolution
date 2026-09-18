using System.Net;
using System.Net.Http.Json;
using GoSit.Domain.Bookings;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

[Collection(nameof(ApiCollection))]
public class BookingTests(GoSitApiFactory factory)
{
    private static readonly DateTime Start = NextWeekdayAtUtc(DayOfWeek.Wednesday, 15);
    private static readonly DateTime End = Start.AddHours(4);

    private async Task<(HttpClient Client, Guid RecipientId)> RequesterAsync()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();
        var recipientId = await client.AddCareRecipientAsync();
        return (client, recipientId);
    }

    [Fact]
    public async Task A_requester_can_book_a_qualified_caregiver()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();

        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        Assert.Equal("Pending", booking.Status);
        Assert.Equal(caregiverId, booking.CaregiverProfileId);
        Assert.Equal("BABYSITTING", booking.CareServiceCode);
        Assert.Single(booking.Recipients);
    }

    /// <summary>
    /// The price is derived from the caregiver's stored rate. A client cannot send one,
    /// and the API must not accept one smuggled into the payload.
    /// </summary>
    [Fact]
    public async Task The_total_is_computed_from_the_caregivers_rate_not_the_request()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync(hourlyRate: 40m);
        var (requester, recipientId) = await RequesterAsync();

        var response = await requester.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = caregiverId,
            careServiceCode = "BABYSITTING",
            careRecipientIds = new[] { recipientId },
            startAtUtc = Start,
            endAtUtc = End,
            address = (object?)null,
            instructions = (string?)null,
            // Attempts to dictate the price. Both must be ignored.
            hourlyRate = 1m,
            estimatedTotal = 4m,
        });

        response.EnsureSuccessStatusCode();
        var booking = (await response.Content.ReadFromJsonAsync<BookingBody>())!;

        Assert.Equal(40m, booking.HourlyRate);
        Assert.Equal(160m, booking.EstimatedTotal);
    }

    [Fact]
    public async Task A_caregiver_missing_a_required_certification_cannot_be_booked()
    {
        // Offers overnight care but holds only one of its three certifications.
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["OVERNIGHT_CHILD_CARE"],
            certificationCodes: ["VULNERABLE_SECTOR"],
            availability: [(DayOfWeek.Wednesday, "00:00:00", "23:59:00")]);

        var (requester, recipientId) = await RequesterAsync();

        var response = await requester.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = caregiverId,
            careServiceCode = "OVERNIGHT_CHILD_CARE",
            careRecipientIds = new[] { recipientId },
            startAtUtc = Start,
            endAtUtc = End,
            address = (object?)null,
            instructions = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_service_that_does_not_suit_the_recipient_is_rejected()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["SENIOR_COMPANIONSHIP"],
            certificationCodes: ["POLICE_CHECK"],
            availability: [(DayOfWeek.Wednesday, "00:00:00", "23:59:00")]);

        var (requester, recipientId) = await RequesterAsync();

        // The recipient is a child; senior companionship does not apply.
        var response = await requester.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = caregiverId,
            careServiceCode = "SENIOR_COMPANIONSHIP",
            careRecipientIds = new[] { recipientId },
            startAtUtc = Start,
            endAtUtc = End,
            address = (object?)null,
            instructions = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// A requester must not be able to name someone else's care recipient on a booking.
    /// </summary>
    [Fact]
    public async Task Another_requesters_care_recipient_cannot_be_booked_for()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (_, theirRecipientId) = await RequesterAsync();
        var (attacker, _) = await RequesterAsync();

        var response = await attacker.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = caregiverId,
            careServiceCode = "BABYSITTING",
            careRecipientIds = new[] { theirRecipientId },
            startAtUtc = Start,
            endAtUtc = End,
            address = (object?)null,
            instructions = (string?)null,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_unverified_caregiver_cannot_be_booked()
    {
        var caregiver = await factory.RegisterCaregiverAsync();
        var created = await caregiver.PutAsJsonAsync("/api/v1/caregivers/me", new
        {
            bio = "Ready.",
            yearsOfExperience = 3,
            hourlyRate = 25m,
            address = new
            {
                line1 = "1 Yonge St",
                line2 = (string?)null,
                city = "Toronto",
                province = "ON",
                postalCode = "M5E 1E5",
                country = "CA",
            },
            serviceRadiusKm = 20,
        });
        var unverifiedId = (await created.Content.ReadFromJsonAsync<SearchTestFixtures.CaregiverBody>())!.Id;

        var (requester, recipientId) = await RequesterAsync();

        var response = await requester.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = unverifiedId,
            careServiceCode = "BABYSITTING",
            careRecipientIds = new[] { recipientId },
            startAtUtc = Start,
            endAtUtc = End,
            address = (object?)null,
            instructions = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_caregiver_cannot_be_double_booked()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (first, firstRecipient) = await RequesterAsync();
        var (second, secondRecipient) = await RequesterAsync();

        await first.CreateBookingAsync(caregiverId, firstRecipient, Start, End);

        var overlapping = await second.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = caregiverId,
            careServiceCode = "BABYSITTING",
            careRecipientIds = new[] { secondRecipient },
            startAtUtc = Start.AddHours(1),
            endAtUtc = End.AddHours(1),
            address = (object?)null,
            instructions = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Conflict, overlapping.StatusCode);
    }

    [Fact]
    public async Task A_caregiver_cannot_create_a_booking()
    {
        var caregiver = await factory.RegisterCaregiverAsync();

        var response = await caregiver.PostAsJsonAsync("/api/v1/bookings", new
        {
            caregiverProfileId = Guid.NewGuid(),
            careServiceCode = "BABYSITTING",
            careRecipientIds = new[] { Guid.NewGuid() },
            startAtUtc = Start,
            endAtUtc = End,
            address = (object?)null,
            instructions = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_assigned_caregiver_can_accept_a_booking()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var response = await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accepted = (await response.Content.ReadFromJsonAsync<BookingBody>())!;
        Assert.Equal("Accepted", accepted.Status);
    }

    /// <summary>
    /// A requester must not be able to accept on the caregiver's behalf.
    /// </summary>
    [Fact]
    public async Task A_requester_cannot_accept_their_own_booking()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var response = await requester.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(BookingStatus.Pending, await factory.ReadStatusAsync(booking.Id));
    }

    /// <summary>
    /// A booking is private to its two participants, and an outsider must not learn that
    /// the id exists.
    /// </summary>
    [Fact]
    public async Task An_unrelated_caregiver_cannot_see_or_accept_a_booking()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var otherCaregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var stranger = await factory.CaregiverClientForAsync(otherCaregiverId);

        var read = await stranger.GetAsync($"/api/v1/bookings/{booking.Id}");
        var accept = await stranger.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
        Assert.Equal(BookingStatus.Pending, await factory.ReadStatusAsync(booking.Id));
    }

    [Fact]
    public async Task An_unrelated_requester_cannot_see_a_booking()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var (stranger, _) = await RequesterAsync();

        var response = await stranger.GetAsync($"/api/v1/bookings/{booking.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_caregiver_can_decline_with_a_reason()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var response = await caregiver.PostAsJsonAsync(
            $"/api/v1/bookings/{booking.Id}/decline", new { reason = "Already busy." });

        var declined = (await response.Content.ReadFromJsonAsync<BookingBody>())!;
        Assert.Equal("Declined", declined.Status);
        Assert.Equal("Already busy.", declined.ResolutionReason);
    }

    [Fact]
    public async Task A_booking_cannot_be_accepted_after_it_was_declined()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        await caregiver.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/decline", new { reason = "No." });

        var response = await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Who cancelled is derived from the caller, not the request body — otherwise a
    /// requester could book a late-cancellation fee to the caregiver.
    /// </summary>
    [Fact]
    public async Task The_cancelling_party_is_derived_from_the_caller()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var byRequester = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var response = await requester.PostAsJsonAsync(
            $"/api/v1/bookings/{byRequester.Id}/cancel",
            new { reason = "Plans changed.", cancelledBy = "Caregiver" });

        var cancelled = (await response.Content.ReadFromJsonAsync<BookingBody>())!;
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("CareRequester", cancelled.CancelledBy);
    }

    [Fact]
    public async Task A_caregiver_can_also_cancel()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var response = await caregiver.PostAsJsonAsync(
            $"/api/v1/bookings/{booking.Id}/cancel", new { reason = "Unwell." });

        var cancelled = (await response.Content.ReadFromJsonAsync<BookingBody>())!;
        Assert.Equal("Caregiver", cancelled.CancelledBy);
    }

    [Fact]
    public async Task The_full_lifecycle_runs_from_request_to_completion()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync(hourlyRate: 30m);
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);

        // Bring the window into the present so check-in and check-out are permitted.
        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - Start);

        var checkedIn = await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null);
        Assert.Equal(HttpStatusCode.OK, checkedIn.StatusCode);
        Assert.Equal(BookingStatus.InProgress, await factory.ReadStatusAsync(booking.Id));

        var checkedOut = await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-out", null);
        Assert.Equal(HttpStatusCode.OK, checkedOut.StatusCode);

        var completed = (await checkedOut.Content.ReadFromJsonAsync<BookingBody>())!;
        Assert.Equal("Completed", completed.Status);
        Assert.NotNull(completed.CheckedInAtUtc);
        Assert.NotNull(completed.CheckedOutAtUtc);
        // Billed on time actually worked, which here is minutes rather than four hours.
        Assert.NotNull(completed.ActualTotal);
        Assert.True(completed.ActualTotal < completed.EstimatedTotal);
    }

    [Fact]
    public async Task A_requester_cannot_check_in()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);
        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - Start);

        var response = await requester.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_pending_booking_cannot_be_checked_in()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);
        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - Start);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var response = await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Both_parties_can_list_the_booking_from_their_own_side()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var requesterList = await requester.GetFromJsonAsync<List<BookingBody>>("/api/v1/bookings");

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var caregiverList = await caregiver.GetFromJsonAsync<List<BookingBody>>("/api/v1/bookings");

        Assert.Contains(requesterList!, b => b.Id == booking.Id);
        Assert.Contains(caregiverList!, b => b.Id == booking.Id);
    }

    [Fact]
    public async Task Bookings_can_be_filtered_by_status()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var pending = await requester.GetFromJsonAsync<List<BookingBody>>(
            "/api/v1/bookings?status=Pending");
        var completed = await requester.GetFromJsonAsync<List<BookingBody>>(
            "/api/v1/bookings?status=Completed");

        Assert.Contains(pending!, b => b.Id == booking.Id);
        Assert.DoesNotContain(completed!, b => b.Id == booking.Id);
    }

    [Fact]
    public async Task An_unrecognized_status_filter_is_rejected()
    {
        var (requester, _) = await RequesterAsync();

        var response = await requester.GetAsync("/api/v1/bookings?status=Nonsense");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The outbox is written in the same transaction as the state change, so every
    /// transition leaves a publishable event behind.
    /// </summary>
    [Fact]
    public async Task Each_transition_writes_an_outbox_message_and_an_audit_row()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);

        var events = await factory.ReadOutboxTypesAsync(booking.Id);
        var audit = await factory.ReadAuditActionsAsync(booking.Id);

        Assert.Equal(
            new[] { "BookingRequestedDomainEvent", "BookingAcceptedDomainEvent" }, events);
        Assert.Equal(new[] { "BookingRequested", "BookingAccepted" }, audit);
    }

    /// <summary>
    /// Events and the audit trail carry identifiers, never the address, instructions,
    /// or anything about the care recipient.
    /// </summary>
    [Fact]
    public async Task Outbox_payloads_carry_no_personal_data()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var payloads = await factory.ReadOutboxPayloadsAsync(booking.Id);

        Assert.NotEmpty(payloads);

        foreach (var payload in payloads)
        {
            Assert.DoesNotContain("Bloor", payload);
            Assert.DoesNotContain("Bedtime", payload);
            Assert.DoesNotContain("Sam", payload);
            Assert.DoesNotContain("@example.com", payload);
            Assert.DoesNotContain(recipientId.ToString(), payload);
        }
    }

    // --- The platform sweep ----------------------------------------------------------
    //
    // Two states no participant can leave on their own: an in-progress booking past the
    // point where check-out is allowed, and a request nobody answered before it was due
    // to start. BookingSweeper closes both. The sweep is driven explicitly here rather
    // than waited for, so these tests do not depend on its poll interval.

    /// <summary>
    /// A caregiver who checks in and never checks out cannot close the booking once the
    /// maximum duration has passed — check-out refuses. Without the sweep the booking
    /// stays InProgress forever.
    /// </summary>
    [Fact]
    public async Task A_booking_the_caregiver_never_checked_out_of_is_completed_by_the_sweep()
    {
        var (bookingId, caregiver) = await StrandedInProgressBookingAsync();

        // The caregiver cannot rescue it themselves any more.
        var refused = await caregiver.PostAsync($"/api/v1/bookings/{bookingId}/check-out", null);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        await factory.RunBookingSweepAsync();

        Assert.Equal(BookingStatus.Completed, await factory.ReadStatusAsync(bookingId));
    }

    /// <summary>
    /// The requester pays the schedule they agreed to, not the hours between the
    /// caregiver walking away and the sweep noticing.
    /// </summary>
    [Fact]
    public async Task An_auto_completed_booking_is_billed_to_its_scheduled_end()
    {
        var (bookingId, caregiver) = await StrandedInProgressBookingAsync();

        await factory.RunBookingSweepAsync();

        var booking = await caregiver.GetFromJsonAsync<BookingBody>($"/api/v1/bookings/{bookingId}");

        Assert.Equal("Completed", booking!.Status);
        Assert.True(booking.WasAutoCompleted);
        // Nobody checked out, so no check-out time is recorded.
        Assert.Null(booking.CheckedOutAtUtc);
        // Four scheduled hours at 30/hour, not the fifteen since check-in.
        Assert.Equal(120m, booking.ActualTotal);
    }

    [Fact]
    public async Task The_sweep_leaves_a_booking_the_caregiver_can_still_check_out_of()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);
        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - Start);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null);

        await factory.RunBookingSweepAsync();

        // Still theirs to close: the sweep only takes over once check-out is refused.
        Assert.Equal(BookingStatus.InProgress, await factory.ReadStatusAsync(booking.Id));
    }

    [Fact]
    public async Task A_request_nobody_answered_is_cancelled_by_the_platform()
    {
        var (bookingId, _) = await UnansweredRequestAsync();

        await factory.RunBookingSweepAsync();

        Assert.Equal(BookingStatus.Cancelled, await factory.ReadStatusAsync(bookingId));
    }

    /// <summary>
    /// Attributed to the platform, not to either participant — Payments must be able to
    /// tell an unanswered request apart from a cancellation someone chose to make.
    /// </summary>
    [Fact]
    public async Task An_unanswered_request_is_cancelled_by_neither_participant()
    {
        var (bookingId, requester) = await UnansweredRequestAsync();

        await factory.RunBookingSweepAsync();

        var booking = await requester.GetFromJsonAsync<BookingBody>($"/api/v1/bookings/{bookingId}");

        Assert.Equal("Cancelled", booking!.Status);
        Assert.Equal("Platform", booking.CancelledBy);
    }

    [Fact]
    public async Task The_sweep_leaves_a_pending_request_whose_start_has_not_passed()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        await factory.RunBookingSweepAsync();

        Assert.Equal(BookingStatus.Pending, await factory.ReadStatusAsync(booking.Id));
    }

    /// <summary>
    /// A sweep closes a booking the same way a request would: the event goes through the
    /// outbox and the action is audited, with no acting user because the platform acted.
    /// </summary>
    [Fact]
    public async Task A_swept_booking_writes_an_outbox_message_and_an_audit_row()
    {
        var (bookingId, _) = await StrandedInProgressBookingAsync();

        await factory.RunBookingSweepAsync();

        var events = await factory.ReadOutboxTypesAsync(bookingId);
        var audit = await factory.ReadAuditActionsAsync(bookingId);

        Assert.Equal("BookingCompletedDomainEvent", events[^1]);
        Assert.Equal("BookingAutoCompleted", audit[^1]);
    }

    [Fact]
    public async Task A_sweep_that_finds_nothing_changes_nothing()
    {
        await factory.RunBookingSweepAsync();

        var result = await factory.RunBookingSweepAsync();

        Assert.Equal(0, result.Total);
    }

    /// <summary>
    /// A booking checked into fifteen hours ago and never checked out — past the
    /// fourteen-hour ceiling, so the caregiver can no longer close it.
    /// </summary>
    private async Task<(Guid BookingId, HttpClient Caregiver)> StrandedInProgressBookingAsync()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync(hourlyRate: 30m);
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);

        // Check in for real, then backdate the whole engagement. Both the schedule and
        // the check-in move, so the row looks exactly like one abandoned overnight.
        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - Start);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null);

        var backdate = TimeSpan.FromHours(-15);
        await factory.ShiftScheduleAsync(booking.Id, backdate);
        await factory.ShiftCheckInAsync(booking.Id, backdate);

        return (booking.Id, caregiver);
    }

    /// <summary>A request left pending until well after it was due to start.</summary>
    private async Task<(Guid BookingId, HttpClient Requester)> UnansweredRequestAsync()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        // An hour past its start, comfortably beyond the fifteen-minute grace.
        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow.AddHours(-1) - Start);

        return (booking.Id, requester);
    }

    [Fact]
    public async Task Booking_endpoints_require_authentication()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/bookings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
