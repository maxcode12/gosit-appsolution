using GoSit.Domain.Bookings;
using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;

namespace GoSit.Application.Abstractions;

public sealed record BookingListFilter(BookingStatus? Status, bool UpcomingOnly);

public interface IBookingRepository
{
    /// <summary>
    /// Loads a booking with its recipients. Callers must still check participation —
    /// unlike the profile repositories, this cannot be scoped by owner, because both
    /// sides of a booking legitimately read the same row.
    /// </summary>
    Task<CareBooking?> FindByIdAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CareBooking>> ListForCareRequesterAsync(
        Guid careRequesterProfileId,
        BookingListFilter filter,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CareBooking>> ListForCaregiverAsync(
        Guid caregiverProfileId,
        BookingListFilter filter,
        DateTime utcNow,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the caregiver already has a live booking overlapping this window.
    /// Live means pending, accepted, or in progress — a declined or cancelled booking
    /// does not hold the slot.
    /// </summary>
    Task<bool> HasOverlappingBookingAsync(
        Guid caregiverProfileId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        Guid? excludingBookingId,
        CancellationToken cancellationToken);

    void Add(CareBooking booking);

    /// <summary>Loads a caregiver with everything eligibility checks need.</summary>
    Task<CaregiverProfile?> FindCaregiverForBookingAsync(
        Guid caregiverProfileId,
        CancellationToken cancellationToken);

    Task<CareService?> FindServiceByIdAsync(Guid careServiceId, CancellationToken cancellationToken);
}

public interface IBookingLookupRepository
{
    /// <summary>Recipient details for the ids named on a booking.</summary>
    Task<IReadOnlyList<Domain.CareRecipients.CareRecipient>> ListRecipientsAsync(
        IReadOnlyCollection<Guid> careRecipientIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Pets named on bookings, for their care notes. Asked only for bookings whose
    /// household details the viewer may see.
    /// </summary>
    Task<IReadOnlyList<Domain.Pets.Pet>> ListPetsAsync(
        IReadOnlyCollection<Guid> petIds,
        CancellationToken cancellationToken);

    /// <summary>Service code and name for the ids on a set of bookings.</summary>
    Task<IReadOnlyDictionary<Guid, CareService>> ListServicesAsync(
        IReadOnlyCollection<Guid> careServiceIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// When a booking completed, or null if it has not.
    ///
    /// Payouts are held until the dispute window closes, and that window runs from
    /// completion rather than from settlement — so this is what decides when a caregiver
    /// is paid.
    /// </summary>
    Task<DateTime?> FindCompletedAtAsync(
        Guid careBookingId,
        CancellationToken cancellationToken);

    /// <summary>Maps caregiver profile ids to the owning account, for name lookup.</summary>
    Task<IReadOnlyDictionary<Guid, Guid>> ListCaregiverUserIdsAsync(
        IReadOnlyCollection<Guid> caregiverProfileIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Maps care-requester profile ids to the owning account, for name lookup. The
    /// caregiver side of a booking needs to know which family booked them; without this
    /// a booking names only the caregiver, which tells them nothing.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid>> ListCareRequesterUserIdsAsync(
        IReadOnlyCollection<Guid> careRequesterProfileIds,
        CancellationToken cancellationToken);
}
