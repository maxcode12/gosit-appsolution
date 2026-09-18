using GoSit.Domain.CareServices;

namespace GoSit.Domain.Bookings;

/// <summary>
/// Booking rules that are policy rather than invariants — the numbers a product owner
/// would expect to tune. Kept together so they are visible in one place instead of
/// scattered through the aggregate.
/// </summary>
public static class BookingPolicy
{
    /// <summary>A booking must start at least this far ahead, leaving time to answer.</summary>
    public static readonly TimeSpan MinimumNotice = TimeSpan.FromHours(2);

    /// <summary>How far ahead care can be booked.</summary>
    public static readonly TimeSpan MaximumLeadTime = TimeSpan.FromDays(180);

    public static readonly TimeSpan MinimumDuration = TimeSpan.FromHours(1);

    /// <summary>
    /// The shortest pet-care booking. A dog walk or a drop-in visit is commonly half an
    /// hour, and the one-hour floor would make pet care unbookable at the length people
    /// actually buy it.
    /// </summary>
    public static readonly TimeSpan PetCareMinimumDuration = TimeSpan.FromMinutes(30);

    public static TimeSpan MinimumDurationFor(CareService service) =>
        service.IsPetCare ? PetCareMinimumDuration : MinimumDuration;

    /// <summary>The longest stay priced per night. A month covers holidays; longer is a different arrangement.</summary>
    public const int MaximumNights = 30;

    /// <summary>
    /// How long past its scheduled pick-up a stay can still be closed by the caregiver.
    /// Owners are late back from trips, and closing a stay the moment the pick-up time
    /// passes would bill a booking that is still going on.
    /// </summary>
    public static readonly TimeSpan LatePickupGrace = TimeSpan.FromDays(1);

    /// <summary>
    /// Nights between drop-off and pick-up: the local dates crossed. Friday evening to
    /// Sunday morning is two nights, whatever the hours.
    ///
    /// Counted in the operating time zone — the one availability uses — so a drop-off late
    /// on a Friday in Toronto counts from Friday, though in UTC it is already Saturday.
    /// </summary>
    public static int NightsBetween(DateTime startUtc, DateTime endUtc)
    {
        var start = TimeZoneInfo.ConvertTimeFromUtc(startUtc, OperatingTimeZone.Value);
        var end = TimeZoneInfo.ConvertTimeFromUtc(endUtc, OperatingTimeZone.Value);

        return (end.Date - start.Date).Days;
    }

    private static readonly Lazy<TimeZoneInfo> OperatingTimeZone =
        new(() => TimeZoneInfo.FindSystemTimeZoneById("America/Toronto"));

    /// <summary>A single booking cannot run longer than this; overnight care still fits.</summary>
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(14);

    /// <summary>
    /// Cancelling inside this window counts as a late cancellation. It does not block
    /// the cancellation — it records it, so Payments can apply a fee in Phase 6.
    /// </summary>
    public static readonly TimeSpan LateCancellationWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// How early a caregiver may check in. Arriving a little early is normal; checking
    /// in hours ahead is not, and would distort the billed duration.
    /// </summary>
    public static readonly TimeSpan CheckInGrace = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How long past its start an unanswered booking is left alone before the platform
    /// closes it. Short deliberately: once the start time has gone by, a pending request
    /// is no longer something either side can act on, and leaving it listed as actionable
    /// is worse than closing it.
    /// </summary>
    public static readonly TimeSpan UnansweredGrace = TimeSpan.FromMinutes(15);

    /// <summary>A booking may be disputed for this long after completion.</summary>
    public static readonly TimeSpan DisputeWindow = TimeSpan.FromDays(14);

    /// <summary>
    /// Billing rounds up to the next quarter hour, so a 61-minute visit bills 1.25
    /// hours. Rounding up is the convention caregivers are used to.
    /// </summary>
    public static readonly TimeSpan BillingIncrement = TimeSpan.FromMinutes(15);
}
