using GoSit.Domain.Audit;
using GoSit.Domain.Bookings;
using GoSit.Domain.CareRecipients;
using GoSit.Domain.CareRequesters;
using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class CareBookingConfiguration : IEntityTypeConfiguration<CareBooking>
{
    public void Configure(EntityTypeBuilder<CareBooking> builder)
    {
        builder.ToTable("care_bookings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Status).IsRequired().HasConversion<int>();
        builder.Property(b => b.StartAtUtc).IsRequired();
        builder.Property(b => b.EndAtUtc).IsRequired();
        builder.Property(b => b.Instructions).HasMaxLength(2000);
        builder.Property(b => b.ResolutionReason).HasMaxLength(500);

        // Money is decimal with explicit precision, never floating point.
        builder.Property(b => b.HourlyRateSnapshot).IsRequired().HasPrecision(10, 2);
        builder.Property(b => b.EstimatedTotal).IsRequired().HasPrecision(10, 2);
        builder.Property(b => b.ActualTotal).HasPrecision(10, 2);
        builder.Property(b => b.NightlyRateSnapshot).HasPrecision(10, 2);
        builder.Property(b => b.PricingUnit).IsRequired().HasConversion<int>();
        builder.Property(b => b.CareLocation).IsRequired().HasConversion<int>();

        builder.Property(b => b.CancelledBy).HasConversion<int?>();
        builder.Property(b => b.IsLateCancellation).IsRequired();
        builder.Property(b => b.WasAutoCompleted).IsRequired();

        // Stored as a status and a distance, never a coordinate — see ArrivalEvidence.
        builder.Property(b => b.CheckInVerification).IsRequired().HasConversion<int>();
        builder.Property(b => b.CheckOutVerification).IsRequired().HasConversion<int>();
        builder.Property(b => b.CheckInDistanceMetres);
        builder.Property(b => b.CheckOutDistanceMetres);
        builder.Property(b => b.CreatedAtUtc).IsRequired();

        // Explicit concurrency token: a caregiver accepting while the requester cancels
        // must not both succeed. Unlike xmin this is a real column EF projects on read.
        builder.Property(b => b.Version).IsRequired().IsConcurrencyToken();

        builder.OwnsOne(b => b.Address, AddressConfiguration.Configure);
        builder.Navigation(b => b.Address).IsRequired();

        builder.HasOne<CareRequesterProfile>()
            .WithMany()
            .HasForeignKey(b => b.CareRequesterProfileId)
            // Bookings are financial and safety records. Deleting a profile must not
            // silently erase them; closing an account is a Trust and Safety workflow.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CaregiverProfile>()
            .WithMany()
            .HasForeignKey(b => b.CaregiverProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CareService>()
            .WithMany()
            .HasForeignKey(b => b.CareServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.Recipients)
            .WithOne()
            .HasForeignKey(r => r.CareBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.Recipients).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The two list views: a requester's bookings and a caregiver's, both by date.
        builder.HasIndex(b => new { b.CareRequesterProfileId, b.StartAtUtc });
        builder.HasIndex(b => new { b.CaregiverProfileId, b.StartAtUtc });

        // Supports the sweep for bookings needing attention (unanswered, overrunning).
        builder.HasIndex(b => new { b.Status, b.StartAtUtc });

        // Dispute lifecycle: raised, and settled by staff.
        builder.Property(b => b.DisputedAtUtc);
        builder.Property(b => b.DisputeResolvedAtUtc);
        builder.Property(b => b.DisputeResolutionNotes).HasMaxLength(2000);

        builder.Ignore(b => b.DomainEvents);
    }
}

public sealed class BookingRecipientConfiguration : IEntityTypeConfiguration<BookingRecipient>
{
    public void Configure(EntityTypeBuilder<BookingRecipient> builder)
    {
        builder.ToTable("booking_recipients");

        builder.HasKey(r => r.Id);

        builder.HasIndex(r => new { r.CareBookingId, r.CareRecipientId }).IsUnique();

        builder.HasOne<CareRecipient>()
            .WithMany()
            .HasForeignKey(r => r.CareRecipientId)
            // A recipient named on a booking cannot be deleted out from under it.
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
        builder.Property(a => a.OccurredAtUtc).IsRequired();
        builder.Property(a => a.CorrelationId).HasMaxLength(64);

        // "What happened to this booking" and "what did this account do".
        builder.HasIndex(a => new { a.EntityType, a.EntityId, a.OccurredAtUtc });
        builder.HasIndex(a => a.ActorUserId);

        builder.Ignore(a => a.DomainEvents);
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.OccurredAtUtc).IsRequired();
        builder.Property(m => m.AttemptCount).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(1000);

        // The publisher's only query: unprocessed messages, oldest first. Filtered so
        // the index stays small as processed messages accumulate.
        builder.HasIndex(m => m.OccurredAtUtc)
            .HasFilter("\"ProcessedAtUtc\" IS NULL");

        builder.Ignore(m => m.DomainEvents);
    }
}
