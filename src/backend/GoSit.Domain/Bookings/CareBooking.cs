using GoSit.Domain.Bookings.Events;
using GoSit.Domain.CareRecipients;
using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Common.ValueObjects;
using GoSit.Domain.Common;
using GoSit.Domain.Pets;

namespace GoSit.Domain.Bookings;

/// <summary>
/// A care engagement between a care requester and a caregiver.
///
/// This is the aggregate the whole marketplace turns on, so every rule lives here
/// rather than in a handler:
///
/// - status transitions follow exactly the model in CLAUDE.md;
/// - each transition checks the actor, so a caregiver cannot cancel as the requester;
/// - the hourly rate is snapshotted at request time and never re-read from the
///   caregiver's profile, so a later rate change cannot alter an agreed price;
/// - money is only ever computed here, never accepted from a client.
///
/// A "care request" in PLAN.md is a booking in <see cref="BookingStatus.Pending"/>.
/// Open requests broadcast to several caregivers are not part of the MVP; adding them
/// later does not change this aggregate.
/// </summary>
public sealed class CareBooking : Entity
{
    private const int MaxInstructionsLength = 2000;
    private const int MaxRecipients = 6;
    private const int MaxPets = 6;

    private readonly List<BookingRecipient> _recipients = [];
    private readonly List<BookingPet> _pets = [];

    private CareBooking()
    {
    }

    public Guid CareRequesterProfileId { get; private set; }

    public Guid CaregiverProfileId { get; private set; }

    public Guid CareServiceId { get; private set; }

    public BookingStatus Status { get; private set; }

    public DateTime StartAtUtc { get; private set; }

    public DateTime EndAtUtc { get; private set; }

    /// <summary>Where care takes place. Disclosed to the caregiver once accepted.</summary>
    public Address Address { get; private set; } = null!;

    public string? Instructions { get; private set; }

    /// <summary>
    /// The caregiver's rate at the moment of request. Frozen deliberately: the price
    /// both sides agreed to must not move when the caregiver edits their profile.
    /// </summary>
    public decimal HourlyRateSnapshot { get; private set; }

    /*
     * How this booking is priced and where it happens, copied from the service when it is
     * made — for the same reason the rate is: what was agreed must not move if the service
     * is later reconfigured.
     */

    public PricingUnit PricingUnit { get; private set; } = PricingUnit.Hourly;

    public CareLocation CareLocation { get; private set; } = CareLocation.RecipientHome;

    /// <summary>The agreed price per night, for a booking priced by the night. Null otherwise.</summary>
    public decimal? NightlyRateSnapshot { get; private set; }

    public decimal EstimatedTotal { get; private set; }

    /// <summary>
    /// Set when the booking completes: from the hours actually worked at check-out, or
    /// from the scheduled end when the platform auto-completed it.
    /// </summary>
    public decimal? ActualTotal { get; private set; }

    public DateTime? CheckedInAtUtc { get; private set; }

    public DateTime? CheckedOutAtUtc { get; private set; }

    /*
     * Whether the caregiver was at the address when they started and finished, and how
     * far off if not.
     *
     * A distance rather than a place, deliberately — the device's coordinates are
     * compared against the booking address and discarded. See ArrivalEvidence for the
     * reasoning, and for why neither of these can ever refuse a check-in.
     */

    public ArrivalVerification CheckInVerification { get; private set; }
        = ArrivalVerification.NotProvided;

    public double? CheckInDistanceMetres { get; private set; }

    public ArrivalVerification CheckOutVerification { get; private set; }
        = ArrivalVerification.NotProvided;

    public double? CheckOutDistanceMetres { get; private set; }

    /// <summary>
    /// True when the platform closed the booking because the caregiver never checked
    /// out. Distinguishes a completion nobody confirmed from one they did, and is why
    /// <see cref="CheckedOutAtUtc"/> can be null on a completed booking.
    /// </summary>
    public bool WasAutoCompleted { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? AcceptedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public CancellationParty? CancelledBy { get; private set; }

    public bool IsLateCancellation { get; private set; }

    /// <summary>Reason given when declining or cancelling. Shown to the other party.</summary>
    public string? ResolutionReason { get; private set; }

    /// <summary>
    /// Optimistic concurrency token, incremented on every transition. Unlike Phase 2's
    /// profiles, interleaved writes here genuinely conflict: a caregiver accepting while
    /// the requester cancels must not both succeed.
    /// </summary>
    public int Version { get; private set; }

    public IReadOnlyCollection<BookingRecipient> Recipients => _recipients.AsReadOnly();

    /// <summary>Pets named on a pet-care booking. Empty for care for people.</summary>
    public IReadOnlyCollection<BookingPet> Pets => _pets.AsReadOnly();

    public TimeSpan ScheduledDuration => EndAtUtc - StartAtUtc;

    /// <summary>Nights booked, for a booking priced by the night.</summary>
    public int ScheduledNights => BookingPolicy.NightsBetween(StartAtUtc, EndAtUtc);

    /// <summary>Whether the booking has reached a state it can never leave.</summary>
    public bool IsFinished =>
        Status is BookingStatus.Declined or BookingStatus.Cancelled or BookingStatus.Completed
            or BookingStatus.Disputed;

    public static CareBooking Request(
        Guid careRequesterProfileId,
        CaregiverProfile caregiver,
        CareService service,
        IReadOnlyCollection<CareRecipient> recipients,
        DateTime startAtUtc,
        DateTime endAtUtc,
        Address address,
        string? instructions,
        DateTime utcNow,
        IReadOnlyCollection<Pet>? pets = null)
    {
        pets ??= Array.Empty<Pet>();

        if (careRequesterProfileId == Guid.Empty)
        {
            throw new DomainException("A care requester is required.");
        }

        EnsureCareIsFor(recipients, pets, careRequesterProfileId, service);

        if (service.PricingUnit == PricingUnit.Nightly)
        {
            EnsureStayIsValid(startAtUtc, endAtUtc, utcNow);

            // Said plainly rather than left to eligibility, whose answer would only be
            // "not qualified" — which is not the problem.
            if (caregiver.NightlyRate is null)
            {
                throw new DomainException(
                    "This caregiver has not set a nightly rate, so they cannot take overnight pet care yet.");
            }
        }
        else
        {
            EnsureScheduleIsValid(startAtUtc, endAtUtc, utcNow, BookingPolicy.MinimumDurationFor(service));
        }

        EnsureCaregiverCanBeBooked(caregiver, service, startAtUtc, endAtUtc, utcNow);

        // Boarding happens at the caregiver's home, so that is the booking's address whatever
        // the caller supplied. Whose address it is decides who may see it — AddressDisclosedTo.
        if (service.CareLocation == CareLocation.CaregiverHome)
        {
            address = caregiver.Address
                ?? throw new DomainException(
                    "This caregiver has no home address on file, so they cannot take boarding.");
        }

        var booking = new CareBooking
        {
            CareRequesterProfileId = careRequesterProfileId,
            CaregiverProfileId = caregiver.Id,
            CareServiceId = service.Id,
            Status = BookingStatus.Pending,
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            Address = address,
            Instructions = CleanInstructions(instructions),
            // Snapshot, never a client-supplied price.
            HourlyRateSnapshot = caregiver.HourlyRate,
            PricingUnit = service.PricingUnit,
            CareLocation = service.CareLocation,
            NightlyRateSnapshot = service.PricingUnit == PricingUnit.Nightly ? caregiver.NightlyRate : null,
            CreatedAtUtc = utcNow,
        };

        booking.EstimatedTotal = booking.PricingUnit == PricingUnit.Nightly
            ? CalculateNightlyTotal(booking.NightlyRateSnapshot!.Value, booking.ScheduledNights)
            : CalculateTotal(caregiver.HourlyRate, endAtUtc - startAtUtc);

        foreach (var recipient in recipients)
        {
            booking._recipients.Add(BookingRecipient.Create(booking.Id, recipient.Id));
        }

        foreach (var pet in pets)
        {
            booking._pets.Add(BookingPet.Create(booking.Id, pet));
        }

        booking.RaiseDomainEvent(new BookingRequestedDomainEvent(
            booking.Id, careRequesterProfileId, caregiver.Id, service.Id, startAtUtc, utcNow));

        return booking;
    }

    /// <summary>
    /// Accepts the booking. Eligibility is re-checked here, not just at request time —
    /// a certification can lapse or be revoked between the two.
    /// </summary>
    public void Accept(CaregiverProfile caregiver, CareService service, DateTime utcNow)
    {
        EnsureCaregiverIs(caregiver.Id);
        EnsureStatusIs(BookingStatus.Pending, "accepted");

        if (utcNow >= StartAtUtc)
        {
            throw new DomainException("This booking can no longer be accepted; it has already started.");
        }

        EnsureCaregiverCanBeBooked(caregiver, service, StartAtUtc, EndAtUtc, utcNow);

        Status = BookingStatus.Accepted;
        AcceptedAtUtc = utcNow;
        Touch();

        RaiseDomainEvent(new BookingAcceptedDomainEvent(
            Id, CareRequesterProfileId, CaregiverProfileId, utcNow));
    }

    public void Decline(Guid caregiverProfileId, string? reason, DateTime utcNow)
    {
        EnsureCaregiverIs(caregiverProfileId);
        EnsureStatusIs(BookingStatus.Pending, "declined");

        Status = BookingStatus.Declined;
        ResolutionReason = CleanReason(reason);
        Touch();

        RaiseDomainEvent(new BookingDeclinedDomainEvent(
            Id, CareRequesterProfileId, CaregiverProfileId, utcNow));
    }

    /// <summary>
    /// Cancels a pending or accepted booking. Either side may cancel; who did, and
    /// whether it was late, are recorded because Payments treats them differently.
    /// </summary>
    public void Cancel(
        CancellationParty party,
        Guid actorProfileId,
        string? reason,
        DateTime utcNow)
    {
        if (Status is not (BookingStatus.Pending or BookingStatus.Accepted))
        {
            throw new DomainException(
                $"A booking that is {Status} can no longer be cancelled.");
        }

        EnsureActorMatchesParty(party, actorProfileId);

        Status = BookingStatus.Cancelled;
        CancelledAtUtc = utcNow;
        CancelledBy = party;
        ResolutionReason = CleanReason(reason);

        // Recorded rather than blocked: a late cancellation is allowed but carries a fee.
        IsLateCancellation = StartAtUtc - utcNow < BookingPolicy.LateCancellationWindow;
        Touch();

        RaiseDomainEvent(new BookingCancelledDomainEvent(
            Id, CareRequesterProfileId, CaregiverProfileId, party, IsLateCancellation, utcNow));
    }

    /// <summary>
    /// Starts the booking, recording where the caregiver was if their device could say.
    ///
    /// <paramref name="arrival"/> is evidence, never a condition: a caregiver whose phone
    /// cannot fix a location still checks in, and the booking says so. See
    /// <see cref="ArrivalEvidence"/>.
    /// </summary>
    public void CheckIn(Guid caregiverProfileId, DateTime utcNow, ArrivalEvidence? arrival = null)
    {
        EnsureCaregiverIs(caregiverProfileId);
        EnsureStatusIs(BookingStatus.Accepted, "started");

        if (utcNow < StartAtUtc - BookingPolicy.CheckInGrace)
        {
            throw new DomainException("It is too early to check in for this booking.");
        }

        Status = BookingStatus.InProgress;
        CheckedInAtUtc = utcNow;

        var evidence = arrival ?? ArrivalEvidence.None;
        CheckInVerification = evidence.Verification;
        CheckInDistanceMetres = evidence.DistanceMetres;

        Touch();

        RaiseDomainEvent(new BookingStartedDomainEvent(
            Id, CareRequesterProfileId, CaregiverProfileId, utcNow));
    }

    /// <summary>
    /// Ends the booking and computes what is actually owed from the hours worked.
    /// This is the only transition into <see cref="BookingStatus.Completed"/>.
    /// </summary>
    public void CheckOut(
        Guid caregiverProfileId,
        DateTime utcNow,
        ArrivalEvidence? departure = null)
    {
        EnsureCaregiverIs(caregiverProfileId);
        EnsureStatusIs(BookingStatus.InProgress, "completed");

        if (CheckedInAtUtc is not { } checkedInAt)
        {
            throw new DomainException("This booking has no check-in time.");
        }

        if (utcNow <= checkedInAt)
        {
            throw new DomainException("Check-out must be after check-in.");
        }

        var worked = utcNow - checkedInAt;

        if (HasOverrun(checkedInAt, utcNow))
        {
            throw new DomainException(
                "This booking has run past the maximum length and needs support to close it.");
        }

        Status = BookingStatus.Completed;
        CheckedOutAtUtc = utcNow;
        CompletedAtUtc = utcNow;
        // A stay is billed for the nights it lasted — at least one, since the pet was cared
        // for overnight — and hourly care for the time actually worked.
        ActualTotal = PricingUnit == PricingUnit.Nightly
            ? CalculateNightlyTotal(
                NightlyRateSnapshot!.Value,
                Math.Max(1, BookingPolicy.NightsBetween(checkedInAt, utcNow)))
            : CalculateTotal(HourlyRateSnapshot, worked);

        var evidence = departure ?? ArrivalEvidence.None;
        CheckOutVerification = evidence.Verification;
        CheckOutDistanceMetres = evidence.DistanceMetres;

        Touch();

        RaiseDomainEvent(new BookingCompletedDomainEvent(
            Id, CareRequesterProfileId, CaregiverProfileId, ActualTotal.Value, utcNow));
    }

    /// <summary>
    /// Closes a booking the caregiver checked into but never checked out of.
    ///
    /// Once <see cref="HasOverrun"/> is true the caregiver can no longer close it
    /// themselves — <see cref="CheckOut"/> refuses on the same test — so without this the
    /// booking would sit in <see cref="BookingStatus.InProgress"/> forever. Performed by
    /// the platform, so no actor is checked: there is no participant left who is able
    /// to act.
    /// </summary>
    public void AutoComplete(DateTime utcNow)
    {
        EnsureStatusIs(BookingStatus.InProgress, "completed");

        if (CheckedInAtUtc is not { } checkedInAt)
        {
            throw new DomainException("This booking has no check-in time.");
        }

        // Only once the caregiver has genuinely lost the ability to check out. Before
        // that the booking is theirs to close, and closing it for them would bill a
        // duration nobody has confirmed.
        if (!HasOverrun(checkedInAt, utcNow))
        {
            throw new DomainException("This booking can still be checked out by the caregiver.");
        }

        Status = BookingStatus.Completed;
        CompletedAtUtc = utcNow;
        WasAutoCompleted = true;

        // CheckedOutAtUtc stays null on purpose. Nobody checked out, and stamping a time
        // the caregiver never gave would put invented evidence into a financial record.

        // Billed to the schedule both sides agreed to, not the time elapsed since check-in.
        // The caregiver almost certainly left at some unrecorded point, so the hours since
        // check-in are not hours worked, and the requester must not pay for a forgotten
        // check-out.
        var billable = EndAtUtc - checkedInAt;

        // A caregiver who checked in after the booking was already due to end leaves no
        // defensible window to bill. Zero rather than a guess; either side can still
        // dispute within the usual window.
        ActualTotal = billable <= TimeSpan.Zero
            ? 0m
            : PricingUnit == PricingUnit.Nightly
                ? CalculateNightlyTotal(
                    NightlyRateSnapshot!.Value,
                    Math.Max(1, BookingPolicy.NightsBetween(checkedInAt, EndAtUtc)))
                : CalculateTotal(HourlyRateSnapshot, billable);

        Touch();

        RaiseDomainEvent(new BookingCompletedDomainEvent(
            Id, CareRequesterProfileId, CaregiverProfileId, ActualTotal.Value, utcNow));
    }

    /// <summary>
    /// When the dispute was raised.
    ///
    /// Recorded because the staff queue orders by it — a dispute sitting unanswered is a
    /// caregiver unpaid and a family unanswered at once, and the age of the oldest one is
    /// the number that should make someone uncomfortable.
    /// </summary>
    public DateTime? DisputedAtUtc { get; private set; }

    /// <summary>When staff settled the dispute, or null while it is still open.</summary>
    public DateTime? DisputeResolvedAtUtc { get; private set; }

    /// <summary>What staff decided, in their own words. Shown to both parties.</summary>
    public string? DisputeResolutionNotes { get; private set; }

    /// <summary>
    /// Records the staff decision on a dispute.
    ///
    /// The booking stays <see cref="BookingStatus.Disputed"/> rather than reverting to
    /// Completed. It was disputed; that is a fact about what happened, and a status that
    /// erased it would hide the one thing anyone looking back at this booking needs to
    /// know. What changes is that it is settled — and what settles it is the money, which
    /// the handler moves.
    /// </summary>
    public void ResolveDispute(string notes, DateTime utcNow)
    {
        EnsureStatusIs(BookingStatus.Disputed, "resolved");

        if (DisputeResolvedAtUtc is not null)
        {
            throw new DomainException("That dispute has already been settled.");
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            // Both sides are told the outcome, and "your dispute was closed" with no
            // reason is worse than no message at all.
            throw new DomainException("A resolution note is required.");
        }

        DisputeResolvedAtUtc = utcNow;
        DisputeResolutionNotes = CleanReason(notes);
        Touch();
    }

    /// <summary>Raised by either party within the dispute window after completion.</summary>
    public void Dispute(Guid actorProfileId, string reason, DateTime utcNow)
    {
        EnsureStatusIs(BookingStatus.Completed, "disputed");

        if (actorProfileId != CareRequesterProfileId && actorProfileId != CaregiverProfileId)
        {
            throw new DomainException("Only a participant can dispute this booking.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A reason is required to raise a dispute.");
        }

        if (CompletedAtUtc is { } completedAt
            && utcNow - completedAt > BookingPolicy.DisputeWindow)
        {
            throw new DomainException("The window for disputing this booking has closed.");
        }

        Status = BookingStatus.Disputed;
        DisputedAtUtc = utcNow;
        ResolutionReason = CleanReason(reason);
        Touch();

        RaiseDomainEvent(new BookingDisputedDomainEvent(
            Id, CareRequesterProfileId, CaregiverProfileId, utcNow));
    }

    /// <summary>Whether this profile is allowed to see the booking at all.</summary>
    public bool IsParticipant(Guid profileId) =>
        profileId == CareRequesterProfileId || profileId == CaregiverProfileId;

    public bool Covers(Guid careRecipientId) =>
        _recipients.Any(r => r.CareRecipientId == careRecipientId);

    /// <summary>
    /// Whether the caregiver may see the household details: the exact address, the
    /// family's notes, and care notes for any pets.
    ///
    /// From acceptance onward, and permanently withheld if the request was never accepted.
    /// A request goes to someone who has agreed to nothing yet; a declined or lapsed one
    /// must not leave them holding a door code and the dates a house will be empty.
    /// </summary>
    public bool HouseholdDetailsDisclosedToCaregiver => AcceptedAtUtc is not null;

    /// <summary>
    /// Billable amount, rounded up to the billing increment and then to cents.
    /// The only place a booking total is ever produced.
    /// </summary>
    internal static decimal CalculateTotal(decimal hourlyRate, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new DomainException("A booking must have a positive duration.");
        }

        var increments = Math.Ceiling(
            duration.TotalMinutes / BookingPolicy.BillingIncrement.TotalMinutes);

        var billableHours = (decimal)(increments * BookingPolicy.BillingIncrement.TotalMinutes) / 60m;

        return decimal.Round(hourlyRate * billableHours, 2, MidpointRounding.AwayFromZero);
    }

    internal static decimal CalculateNightlyTotal(decimal nightlyRate, int nights)
    {
        if (nights < 1)
        {
            throw new DomainException("A stay must include at least one night.");
        }

        return decimal.Round(nightlyRate * nights, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Whether the caregiver can no longer close this booking themselves.
    ///
    /// Hourly care runs out of road fourteen hours after check-in. A stay runs out a day
    /// after its scheduled pick-up — measured against the schedule rather than the check-in,
    /// because a three-night stay is meant to last three nights.
    /// </summary>
    private bool HasOverrun(DateTime checkedInAt, DateTime utcNow) =>
        PricingUnit == PricingUnit.Nightly
            ? utcNow > EndAtUtc + BookingPolicy.LatePickupGrace
            : utcNow - checkedInAt > BookingPolicy.MaximumDuration;

    /// <summary>
    /// Whether the booking's exact address may be shown to one side of it.
    ///
    /// Care at the family's home: the family always, the caregiver once they accept. Boarding
    /// at the caregiver's home turns that around — the caregiver's own address is theirs to
    /// see, and the family sees it once the caregiver accepts. A family who has only asked
    /// has no more claim to a stranger's address than a caregiver who has only been asked
    /// has to theirs.
    /// </summary>
    public bool AddressDisclosedTo(bool viewerIsCaregiver) =>
        CareLocation == CareLocation.CaregiverHome
            ? viewerIsCaregiver || AcceptedAtUtc is not null
            : !viewerIsCaregiver || AcceptedAtUtc is not null;

    private static void EnsureStayIsValid(DateTime startAtUtc, DateTime endAtUtc, DateTime utcNow)
    {
        if (startAtUtc.Kind != DateTimeKind.Utc || endAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Booking times must be supplied in UTC.");
        }

        if (endAtUtc <= startAtUtc)
        {
            throw new DomainException("A stay must end after it starts.");
        }

        var nights = BookingPolicy.NightsBetween(startAtUtc, endAtUtc);

        // A stay that starts and ends on the same day is not a night's care. Hourly services
        // — drop-in visits, dog walking — cover it.
        if (nights < 1)
        {
            throw new DomainException(
                "A stay must include at least one night: it ends on a later day than it starts.");
        }

        if (nights > BookingPolicy.MaximumNights)
        {
            throw new DomainException(
                $"A stay can be at most {BookingPolicy.MaximumNights} nights.");
        }

        EnsureWithinBookingHorizon(startAtUtc, utcNow);
    }

    private static void EnsureScheduleIsValid(
        DateTime startAtUtc,
        DateTime endAtUtc,
        DateTime utcNow,
        TimeSpan? minimumDuration = null)
    {
        if (startAtUtc.Kind != DateTimeKind.Utc || endAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Booking times must be supplied in UTC.");
        }

        var duration = endAtUtc - startAtUtc;

        var minimum = minimumDuration ?? BookingPolicy.MinimumDuration;

        if (duration < minimum)
        {
            throw new DomainException(minimum < TimeSpan.FromHours(1)
                ? $"A booking must be at least {minimum.TotalMinutes:0} minutes long."
                : $"A booking must be at least {minimum.TotalHours:0.#} hour(s) long.");
        }

        if (duration > BookingPolicy.MaximumDuration)
        {
            throw new DomainException(
                $"A booking cannot be longer than {BookingPolicy.MaximumDuration.TotalHours:0.#} hours.");
        }

        EnsureWithinBookingHorizon(startAtUtc, utcNow);
    }

    private static void EnsureWithinBookingHorizon(DateTime startAtUtc, DateTime utcNow)
    {
        if (startAtUtc - utcNow < BookingPolicy.MinimumNotice)
        {
            throw new DomainException(
                $"A booking needs at least {BookingPolicy.MinimumNotice.TotalHours:0.#} hours' notice.");
        }

        if (startAtUtc - utcNow > BookingPolicy.MaximumLeadTime)
        {
            throw new DomainException(
                $"A booking cannot be made more than {BookingPolicy.MaximumLeadTime.TotalDays:0} days ahead.");
        }
    }

    /// <summary>
    /// Routes a booking to the rules for who, or what, it is for.
    ///
    /// A booking is care for people or care for pets, never both. A family wanting a nanny
    /// and a dog walk books two things: they are screened differently, and one caregiver may
    /// be cleared for only one of them.
    /// </summary>
    private static void EnsureCareIsFor(
        IReadOnlyCollection<CareRecipient> recipients,
        IReadOnlyCollection<Pet> pets,
        Guid careRequesterProfileId,
        CareService service)
    {
        if (service.IsPetCare)
        {
            if (recipients.Count > 0)
            {
                throw new DomainException($"{service.Name} is care for pets, so it cannot name a person.");
            }

            EnsurePetsAreValid(pets, careRequesterProfileId, service);
            return;
        }

        if (pets.Count > 0)
        {
            throw new DomainException(
                $"{service.Name} is care for people, so it cannot name a pet. Book pet care separately.");
        }

        EnsureRecipientsAreValid(recipients, careRequesterProfileId, service);
    }

    private static void EnsurePetsAreValid(
        IReadOnlyCollection<Pet> pets,
        Guid careRequesterProfileId,
        CareService service)
    {
        if (pets.Count == 0)
        {
            throw new DomainException("A pet care booking must name at least one pet.");
        }

        if (pets.Count > MaxPets)
        {
            throw new DomainException($"A booking can cover at most {MaxPets} pets.");
        }

        if (pets.DistinctBy(p => p.Id).Count() != pets.Count)
        {
            throw new DomainException("The same pet cannot be listed twice.");
        }

        foreach (var pet in pets)
        {
            // One household per booking: every pet must belong to the family booking. That
            // is also what rules out a group walk across several households.
            if (pet.CareRequesterProfileId != careRequesterProfileId)
            {
                throw new DomainException("That pet does not belong to this care requester.");
            }

            // A dog walker is not booked to look after a rabbit.
            if (!service.AppliesTo(pet.Species))
            {
                throw new DomainException($"{service.Name} is not offered for {DescribeSpecies(pet.Species)}.");
            }
        }
    }

    private static string DescribeSpecies(PetSpecies species) => species switch
    {
        PetSpecies.Dog => "dogs",
        PetSpecies.Cat => "cats",
        PetSpecies.SmallAnimal => "small animals",
        PetSpecies.Bird => "birds",
        _ => "that kind of pet",
    };

    private static void EnsureRecipientsAreValid(
        IReadOnlyCollection<CareRecipient> recipients,
        Guid careRequesterProfileId,
        CareService service)
    {
        if (recipients.Count == 0)
        {
            throw new DomainException("A booking must name at least one care recipient.");
        }

        if (recipients.Count > MaxRecipients)
        {
            throw new DomainException($"A booking can cover at most {MaxRecipients} care recipients.");
        }

        if (recipients.DistinctBy(r => r.Id).Count() != recipients.Count)
        {
            throw new DomainException("The same care recipient cannot be listed twice.");
        }

        foreach (var recipient in recipients)
        {
            // The requester can only book care for their own recipients.
            if (recipient.CareRequesterProfileId != careRequesterProfileId)
            {
                throw new DomainException("That care recipient does not belong to this care requester.");
            }

            // Booking senior care for a child would surface the wrong caregiver skills.
            if (!service.AppliesTo(recipient.RecipientType))
            {
                throw new DomainException(
                    $"{service.Name} is not offered for a {recipient.RecipientType} care recipient.");
            }
        }
    }

    private static void EnsureCaregiverCanBeBooked(
        CaregiverProfile caregiver,
        CareService service,
        DateTime startAtUtc,
        DateTime endAtUtc,
        DateTime utcNow)
    {
        if (!caregiver.IsListable)
        {
            throw new DomainException("This caregiver is not currently accepting bookings.");
        }

        // Offering a service is a claim; holding the certifications is what qualifies.
        if (!caregiver.IsEligibleFor(service, DateOnly.FromDateTime(startAtUtc)))
        {
            throw new DomainException($"This caregiver is not qualified for {service.Name}.");
        }

        // A weekly schedule of working hours cannot say "hosting a dog from Friday to Sunday",
        // so a stay is not tested against it. The caregiver decides by accepting, and a stay
        // still cannot overlap anything else they have booked.
        if (service.PricingUnit == PricingUnit.Hourly && !CoversSchedule(caregiver, startAtUtc, endAtUtc))
        {
            throw new DomainException("This caregiver is not available at that time.");
        }

        _ = utcNow;
    }

    /// <summary>
    /// Whether the caregiver's weekly schedule covers the booking window.
    ///
    /// Availability is recorded in the caregiver's local time, so the UTC window is
    /// converted before comparing. Ontario is the launch market; a caregiver-level time
    /// zone is needed before GoSit operates across more than one.
    /// </summary>
    private static bool CoversSchedule(CaregiverProfile caregiver, DateTime startAtUtc, DateTime endAtUtc)
    {
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(startAtUtc, OperatingTimeZone.Value);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(endAtUtc, OperatingTimeZone.Value);

        // A window crossing local midnight used to be refused outright, which made
        // "Overnight Child Care" — a service GoSit sells — impossible to book, and said
        // so in words that blamed the caregiver. The weekly schedule now expresses a
        // shift that runs past midnight, so the comparison is just a comparison.
        return AvailabilityMatching.Covers(caregiver.Availability, localStart, localEnd);
    }

    private static readonly Lazy<TimeZoneInfo> OperatingTimeZone =
        new(() => TimeZoneInfo.FindSystemTimeZoneById("America/Toronto"));

    private void EnsureCaregiverIs(Guid caregiverProfileId)
    {
        if (caregiverProfileId != CaregiverProfileId)
        {
            throw new DomainException("Only the assigned caregiver can perform this action.");
        }
    }

    private void EnsureActorMatchesParty(CancellationParty party, Guid actorProfileId)
    {
        var expected = party switch
        {
            CancellationParty.CareRequester => CareRequesterProfileId,
            CancellationParty.Caregiver => CaregiverProfileId,
            // Platform cancellations are performed by staff, not a booking participant.
            _ => actorProfileId,
        };

        if (actorProfileId != expected)
        {
            throw new DomainException("Only a participant can cancel this booking.");
        }
    }

    private void EnsureStatusIs(BookingStatus required, string action)
    {
        if (Status != required)
        {
            throw new DomainException($"A booking that is {Status} cannot be {action}.");
        }
    }

    private void Touch() => Version++;

    private static string? CleanInstructions(string? instructions)
    {
        if (string.IsNullOrWhiteSpace(instructions))
        {
            return null;
        }

        var trimmed = instructions.Trim();

        if (trimmed.Length > MaxInstructionsLength)
        {
            throw new DomainException(
                $"Instructions must be {MaxInstructionsLength} characters or fewer.");
        }

        return trimmed;
    }

    private static string? CleanReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, 500)];
}
