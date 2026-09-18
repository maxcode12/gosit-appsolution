using GoSit.Domain.Caregivers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class AvailabilitySlotConfiguration : IEntityTypeConfiguration<AvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<AvailabilitySlot> builder)
    {
        builder.ToTable("availability_slots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Day).IsRequired().HasConversion<int>();
        builder.Property(s => s.StartTime).IsRequired();
        builder.Property(s => s.EndTime).IsRequired();

        // Availability search filters by caregiver and day together.
        builder.HasIndex(s => new { s.CaregiverProfileId, s.Day });

        builder.Ignore(s => s.DomainEvents);
    }
}
