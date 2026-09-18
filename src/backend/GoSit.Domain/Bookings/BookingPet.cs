using GoSit.Domain.Common;
using GoSit.Domain.Pets;

namespace GoSit.Domain.Bookings;

/// <summary>
/// A pet named on a booking, as it was when the booking was made.
///
/// Name, species, breed and size are copied rather than joined, for the same reason a
/// booking snapshots the hourly rate: the booking has to keep saying what was agreed after
/// the family edits the pet or removes it, and a removed pet must not be blocked by — or
/// break — the bookings it appears on.
///
/// Care notes are not copied. They are read live, because an owner updating the feeding
/// instructions the night before should reach the sitter.
/// </summary>
public sealed class BookingPet : Entity
{
    private BookingPet()
    {
    }

    public Guid CareBookingId { get; private set; }

    public Guid PetId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public PetSpecies Species { get; private set; }

    public string? Breed { get; private set; }

    public PetSize? Size { get; private set; }

    internal static BookingPet Create(Guid careBookingId, Pet pet) =>
        new()
        {
            CareBookingId = careBookingId,
            PetId = pet.Id,
            Name = pet.Name,
            Species = pet.Species,
            Breed = pet.Breed,
            Size = pet.Size,
        };
}
