using GoSit.Domain.Caregivers;
using GoSit.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class CaregiverProfileConfiguration : IEntityTypeConfiguration<CaregiverProfile>
{
    public void Configure(EntityTypeBuilder<CaregiverProfile> builder)
    {
        builder.ToTable("caregiver_profiles");

        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.UserId).IsUnique();

        // A profile must not outlive the account it belongs to.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.Bio).HasMaxLength(2000);
        builder.Property(p => p.YearsOfExperience).IsRequired();

        // Money is decimal, never floating point.
        builder.Property(p => p.HourlyRate).IsRequired().HasPrecision(10, 2);
        builder.Property(p => p.NightlyRate).HasPrecision(10, 2);

        builder.Property(p => p.ServiceRadiusKm).IsRequired();
        builder.Property(p => p.VerificationStatus).IsRequired().HasConversion<int>();
        builder.Property(p => p.AcceptingWork).IsRequired();
        builder.Property(p => p.AvailableForLastMinuteRequests).IsRequired();

        // The connected-account reference and what the provider says about it. No bank
        // detail is stored — see IPayoutAccountProvider.
        builder.Property(p => p.PayoutAccountId).HasMaxLength(255);
        builder.Property(p => p.PayoutsEnabled).IsRequired();
        builder.Property(p => p.PayoutDetailsSubmitted).IsRequired();
        builder.Property(p => p.PayoutDisabledReason).HasMaxLength(200);

        // Account webhooks identify a caregiver only by this reference, and the payout
        // sweep filters on it every cycle.
        builder.HasIndex(p => p.PayoutAccountId);
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc);

        builder.OwnsOne(p => p.Address, AddressConfiguration.Configure);

        // Search filters on these before anything else, so they are indexed together.
        builder.HasIndex(p => new { p.VerificationStatus, p.AcceptingWork });
        builder.HasIndex(p => p.HourlyRate);

        builder.HasMany(p => p.Availability)
            .WithOne()
            .HasForeignKey(s => s.CaregiverProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Certifications)
            .WithOne()
            .HasForeignKey(c => c.CaregiverProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Services offered. Named explicitly so the join table is ours, not an EF default.
        builder.HasMany(p => p.Services)
            .WithMany()
            .UsingEntity(join => join.ToTable("caregiver_services"));

        builder.Property(p => p.AverageRating).HasPrecision(3, 2);
        builder.Property(p => p.ReviewCount).IsRequired();

        // The Caregiver Service Agreement signature — see docs/BUSINESS_MODEL.md and
        // GoSit.Domain.Caregivers.ServiceAgreement. A typed name and a version stamp,
        // not the document text itself.
        builder.Property(p => p.AgreementAcceptedVersion).HasMaxLength(50);
        builder.Property(p => p.AgreementSignedName).HasMaxLength(200);
        builder.Property(p => p.AgreementAcceptedAtUtc);

        builder.HasMany(p => p.InsurancePolicies)
            .WithOne()
            .HasForeignKey(i => i.CaregiverProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Availability).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.Certifications).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.InsurancePolicies).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);
    }
}

public sealed class CaregiverInsurancePolicyConfiguration : IEntityTypeConfiguration<CaregiverInsurancePolicy>
{
    public void Configure(EntityTypeBuilder<CaregiverInsurancePolicy> builder)
    {
        builder.ToTable("caregiver_insurance_policies");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.CoverageType).IsRequired().HasConversion<int>();
        builder.Property(p => p.Insurer).IsRequired().HasMaxLength(200);
        builder.Property(p => p.PolicyReference).IsRequired().HasMaxLength(100);
        builder.Property(p => p.LiabilityLimit).IsRequired().HasPrecision(12, 2);
        builder.Property(p => p.EffectiveOn).IsRequired();
        builder.Property(p => p.ExpiresOn).IsRequired();
        builder.Property(p => p.Status).IsRequired().HasConversion<int>();
        builder.Property(p => p.ExpiryNoticeStage).IsRequired().HasConversion<int>();

        // A private Blob Storage key. Never projected into a public response.
        builder.Property(p => p.DocumentReference).HasMaxLength(500);

        builder.Property(p => p.ReviewNotes).HasMaxLength(1000);
        builder.Property(p => p.SubmittedAtUtc).IsRequired();
        builder.Property(p => p.ReviewedAtUtc);

        builder.HasIndex(p => p.CaregiverProfileId);

        // Supports the expiry-sweep job, mirroring the certification index.
        builder.HasIndex(p => new { p.Status, p.ExpiresOn });

        builder.Ignore(p => p.DomainEvents);
    }
}

public sealed class CaregiverExposureConfiguration : IEntityTypeConfiguration<CaregiverExposure>
{
    public void Configure(EntityTypeBuilder<CaregiverExposure> builder)
    {
        builder.ToTable("caregiver_exposures");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.CaregiverProfileId).IsRequired();
        builder.Property(e => e.OnDate).IsRequired();
        builder.Property(e => e.Impressions).IsRequired();
        builder.Property(e => e.ProfileViews).IsRequired();

        /*
         * One row per caregiver per day, enforced — the upsert that advances the counts
         * depends on this constraint to decide between inserting and incrementing, so it
         * is load-bearing rather than merely tidy.
         *
         * It is also the shape of the privacy promise: there is nowhere in this table to
         * put a searcher even if someone later wanted to.
         */
        builder.HasIndex(e => new { e.CaregiverProfileId, e.OnDate })
            .IsUnique()
            .HasDatabaseName("ix_caregiver_exposures_profile_date");

        builder.Ignore(e => e.DomainEvents);
    }
}
