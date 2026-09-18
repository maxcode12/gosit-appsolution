using GoSit.Domain.Bookings;
using GoSit.Domain.CareRequesters;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Payments;
using GoSit.Domain.Payouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);

        // Money is decimal with explicit precision. Never floating point.
        builder.Property(p => p.Amount).IsRequired().HasPrecision(10, 2);
        builder.Property(p => p.RefundedAmount).IsRequired().HasPrecision(10, 2);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.Status).IsRequired().HasConversion<int>();
        builder.Property(p => p.ProviderPaymentIntentId).HasMaxLength(255);
        builder.Property(p => p.IdempotencyKey).IsRequired().HasMaxLength(255);
        builder.Property(p => p.FailureCode).HasMaxLength(100);
        builder.Property(p => p.CreatedAtUtc).IsRequired();

        // A webhook and a refund must not interleave and both win.
        builder.Property(p => p.Version).IsRequired().IsConcurrencyToken();

        // One payment per booking.
        builder.HasIndex(p => p.CareBookingId).IsUnique();

        // How webhook handling finds the payment an event refers to.
        builder.HasIndex(p => p.ProviderPaymentIntentId).IsUnique();

        builder.HasOne<CareBooking>()
            .WithMany()
            .HasForeignKey(p => p.CareBookingId)
            // Financial records are never deleted by a cascade.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Refunds)
            .WithOne()
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Refunds).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);
    }
}

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refunds");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ProviderRefundId).IsRequired().HasMaxLength(255);
        builder.Property(r => r.Amount).IsRequired().HasPrecision(10, 2);
        builder.Property(r => r.Reason).HasMaxLength(200);
        builder.Property(r => r.CreatedAtUtc).IsRequired();

        // The same provider refund must never be recorded twice.
        builder.HasIndex(r => r.ProviderRefundId).IsUnique();

        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("payouts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.GrossAmount).IsRequired().HasPrecision(10, 2);
        builder.Property(p => p.PlatformFee).IsRequired().HasPrecision(10, 2);
        builder.Property(p => p.Amount).IsRequired().HasPrecision(10, 2);

        // Enough precision for a rate like 0.1550.
        builder.Property(p => p.CommissionRate).IsRequired().HasPrecision(5, 4);

        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.Status).IsRequired().HasConversion<int>();
        builder.Property(p => p.ProviderTransferId).HasMaxLength(255);

        // Retry bookkeeping for the payout sweep.
        builder.Property(p => p.AttemptCount).IsRequired();

        // Payouts wait out the dispute window, and stop entirely while one is open.
        builder.Property(p => p.AvailableAtUtc).IsRequired();
        builder.Property(p => p.IsHeldForDispute).IsRequired();
        builder.Property(p => p.LastAttemptAtUtc);
        builder.Property(p => p.FailureReason).HasMaxLength(500);
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.Version).IsRequired().IsConcurrencyToken();

        // One payout per booking, so a repeat webhook cannot owe a caregiver twice.
        builder.HasIndex(p => p.CareBookingId).IsUnique();

        // The caregiver's wallet view.
        builder.HasIndex(p => new { p.CaregiverProfileId, p.Status });

        builder.HasIndex(p => p.ProviderTransferId);

        builder.HasOne<CaregiverProfile>()
            .WithMany()
            .HasForeignKey(p => p.CaregiverProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(p => p.DomainEvents);
    }
}

/// <summary>
/// Provider event ids already handled. The unique index is what actually enforces
/// exactly-once processing when two deliveries race.
/// </summary>
public sealed class ProcessedWebhookEventConfiguration
    : IEntityTypeConfiguration<ProcessedWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookEvent> builder)
    {
        builder.ToTable("processed_webhook_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ProviderEventId).IsRequired().HasMaxLength(255);
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.ProcessedAtUtc).IsRequired();

        builder.HasIndex(e => e.ProviderEventId).IsUnique();
    }
}
