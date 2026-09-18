using GoSit.Domain.Caregivers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoSit.Infrastructure.Persistence;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class CertificationConfiguration : IEntityTypeConfiguration<Certification>
{
    public void Configure(EntityTypeBuilder<Certification> builder)
    {
        builder.ToTable("certifications");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.RequiresExpiry).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();

        // Eligibility rules refer to certifications by code, so it must be unique.
        builder.HasIndex(c => c.Code).IsUnique();

        // Seeded as reference data so the list can grow without a code change.
        builder.HasData(CertificationSeed.Entries.Select(e => new
        {
            e.Id,
            e.Code,
            e.Name,
            e.RequiresExpiry,
            IsActive = true,
        }));

        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class CaregiverCertificationConfiguration : IEntityTypeConfiguration<CaregiverCertification>
{
    public void Configure(EntityTypeBuilder<CaregiverCertification> builder)
    {
        builder.ToTable("caregiver_certifications");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.IssuedOn).IsRequired();
        builder.Property(c => c.ExpiresOn);
        builder.Property(c => c.Status).IsRequired().HasConversion<int>();
        builder.Property(c => c.ExpiryNoticeStage).IsRequired().HasConversion<int>();

        // A private Blob Storage key. Never projected into a public response.
        builder.Property(c => c.DocumentReference).HasMaxLength(500);

        builder.Property(c => c.ReviewNotes).HasMaxLength(1000);
        builder.Property(c => c.SubmittedAtUtc).IsRequired();
        builder.Property(c => c.ReviewedAtUtc);

        builder.HasOne(c => c.Certification)
            .WithMany()
            .HasForeignKey(c => c.CertificationId)
            // Reference data must not disappear while caregivers still point at it.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.CaregiverProfileId);

        // Supports the expiry-sweep job that flags certifications about to lapse.
        builder.HasIndex(c => new { c.Status, c.ExpiresOn });

        builder.Ignore(c => c.DomainEvents);
    }
}
