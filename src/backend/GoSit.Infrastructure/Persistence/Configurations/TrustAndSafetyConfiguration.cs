using GoSit.Domain.Bookings;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Reviews;
using GoSit.Domain.TrustAndSafety;
using GoSit.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class ReviewPairConfiguration : IEntityTypeConfiguration<ReviewPair>
{
    public void Configure(EntityTypeBuilder<ReviewPair> builder)
    {
        builder.ToTable("review_pairs");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.CompletedAtUtc).IsRequired();

        // One pair per booking.
        builder.HasIndex(p => p.CareBookingId).IsUnique();

        // The sweep that publishes lone reviews once the window closes.
        builder.HasIndex(p => p.CompletedAtUtc);

        builder.HasOne<CareBooking>()
            .WithMany()
            .HasForeignKey(p => p.CareBookingId)
            // Reviews are part of the trust record and are not cascade-deleted.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Reviews)
            .WithOne()
            .HasForeignKey(r => r.CareBookingId)
            .HasPrincipalKey(p => p.CareBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Reviews).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);
    }
}

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Rating).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(ReviewPolicy.MaxCommentLength);
        builder.Property(r => r.SubjectIsCaregiver).IsRequired();
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.PublishedAtUtc);
        builder.Property(r => r.ReportedAtUtc);
        builder.Property(r => r.ReportReason).HasMaxLength(ReviewPolicy.MaxCommentLength);
        builder.Property(r => r.ReportDismissedAtUtc);
        builder.Property(r => r.HiddenAtUtc);
        builder.Property(r => r.HiddenReason).HasMaxLength(2000);
        builder.Property(r => r.HiddenByUserId);

        // One review per author per booking.
        builder.HasIndex(r => new { r.CareBookingId, r.AuthorProfileId }).IsUnique();

        // Recomputing a subject's rating reads only published reviews, so the filter
        // keeps that index tight.
        builder.HasIndex(r => r.SubjectProfileId)
            .HasFilter("\"PublishedAtUtc\" IS NOT NULL")
            .HasDatabaseName("IX_reviews_SubjectProfileId_Published");

        // Supports the moderation queue, kept small by the filter.
        builder.HasIndex(r => r.ReportedAtUtc)
            .HasFilter("\"ReportedAtUtc\" IS NOT NULL");

        builder.Ignore(r => r.IsPubliclyVisible);
        builder.Ignore(r => r.AwaitsModeration);
        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class VerificationCaseConfiguration : IEntityTypeConfiguration<VerificationCase>
{
    public void Configure(EntityTypeBuilder<VerificationCase> builder)
    {
        builder.ToTable("verification_cases");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CheckType).IsRequired().HasConversion<int>();
        builder.Property(c => c.Status).IsRequired().HasConversion<int>();

        // A private Blob Storage key. Never projected into a response.
        builder.Property(c => c.DocumentReference).HasMaxLength(500);

        builder.Property(c => c.ProviderReference).HasMaxLength(255);
        builder.Property(c => c.ProviderStatus).HasMaxLength(64);
        builder.Property(c => c.ProviderScore).HasMaxLength(32);
        builder.Property(c => c.DecisionNotes).HasMaxLength(1000);

        // Webhooks find their case by the provider's reference.
        builder.HasIndex(c => c.ProviderReference);
        builder.Property(c => c.SubmittedAtUtc).IsRequired();
        builder.Property(c => c.Version).IsRequired().IsConcurrencyToken();

        builder.HasOne<CaregiverProfile>()
            .WithMany()
            .HasForeignKey(c => c.CaregiverProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // "Show me this caregiver's checks" and the staff review queue.
        builder.HasIndex(c => new { c.CaregiverProfileId, c.CheckType });
        builder.HasIndex(c => new { c.Status, c.SubmittedAtUtc });

        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class IncidentReportConfiguration : IEntityTypeConfiguration<IncidentReport>
{
    public void Configure(EntityTypeBuilder<IncidentReport> builder)
    {
        builder.ToTable("incident_reports");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Category).IsRequired().HasConversion<int>();
        builder.Property(r => r.Severity).IsRequired().HasConversion<int>();
        builder.Property(r => r.Status).IsRequired().HasConversion<int>();

        // Sensitive: written by a user about a real person. Never logged or exposed to
        // the reported user.
        builder.Property(r => r.Description).IsRequired().HasMaxLength(4000);

        builder.Property(r => r.ResolutionNotes).HasMaxLength(2000);
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.Version).IsRequired().IsConcurrencyToken();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.ReportedByUserId)
            // A report is evidence and must survive an account being closed.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.SubjectUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The triage queue, and "what has been reported about this account".
        builder.HasIndex(r => new { r.Status, r.Severity, r.CreatedAtUtc });
        builder.HasIndex(r => r.SubjectUserId);
        builder.HasIndex(r => r.ReportedByUserId);

        builder.Ignore(r => r.DomainEvents);
    }
}
