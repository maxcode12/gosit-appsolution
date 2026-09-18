using GoSit.Domain.Common;

namespace GoSit.Domain.Bookings;

/// <summary>
/// Links a booking to one care recipient it covers.
///
/// Stores the id only. The recipient's care needs are read live from their profile, so
/// an update to allergies or triggers is reflected on an existing booking rather than
/// frozen at the moment it was made.
/// </summary>
public sealed class BookingRecipient : Entity
{
    private BookingRecipient()
    {
    }

    public Guid CareBookingId { get; private set; }

    public Guid CareRecipientId { get; private set; }

    internal static BookingRecipient Create(Guid careBookingId, Guid careRecipientId)
    {
        if (careRecipientId == Guid.Empty)
        {
            throw new DomainException("A care recipient is required.");
        }

        return new BookingRecipient
        {
            CareBookingId = careBookingId,
            CareRecipientId = careRecipientId,
        };
    }
}
