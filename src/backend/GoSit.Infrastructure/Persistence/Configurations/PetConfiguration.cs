using GoSit.Domain.Bookings;
using GoSit.Domain.CareRequesters;
using GoSit.Domain.Pets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> builder)
    {
        builder.ToTable("pets");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(Pet.MaxNameLength);
        builder.Property(p => p.Species).IsRequired().HasConversion<int>();
        builder.Property(p => p.Breed).HasMaxLength(Pet.MaxBreedLength);
        builder.Property(p => p.Size).HasConversion<int?>();

        builder.Property(p => p.FeedingInstructions).HasMaxLength(Pet.MaxNoteLength);
        builder.Property(p => p.Medication).HasMaxLength(Pet.MaxNoteLength);
        builder.Property(p => p.Behaviour).HasMaxLength(Pet.MaxNoteLength);
        builder.Property(p => p.VetClinic).HasMaxLength(Pet.MaxNoteLength);
        builder.Property(p => p.OtherNotes).HasMaxLength(Pet.MaxNoteLength);

        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc);

        builder.HasOne<CareRequesterProfile>()
            .WithMany(profile => profile.Pets)
            .HasForeignKey(p => p.CareRequesterProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.CareRequesterProfileId);

        builder.Ignore(p => p.HasCareNotes);
        builder.Ignore(p => p.DomainEvents);
    }
}

public sealed class BookingPetConfiguration : IEntityTypeConfiguration<BookingPet>
{
    public void Configure(EntityTypeBuilder<BookingPet> builder)
    {
        builder.ToTable("booking_pets");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(Pet.MaxNameLength);
        builder.Property(p => p.Species).IsRequired().HasConversion<int>();
        builder.Property(p => p.Breed).HasMaxLength(Pet.MaxBreedLength);
        builder.Property(p => p.Size).HasConversion<int?>();

        builder.HasOne<CareBooking>()
            .WithMany(booking => booking.Pets)
            .HasForeignKey(p => p.CareBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // No foreign key to pets, deliberately. The booking carries its own copy of the
        // pet's details, so a family removing a pet neither fails because of old bookings
        // nor rewrites what those bookings say.
        builder.HasIndex(p => new { p.CareBookingId, p.PetId }).IsUnique();

        builder.Ignore(p => p.DomainEvents);
    }
}
