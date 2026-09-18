using GoSit.Domain.CareRequesters;
using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class CareServiceCategoryConfiguration : IEntityTypeConfiguration<CareServiceCategory>
{
    public void Configure(EntityTypeBuilder<CareServiceCategory> builder)
    {
        builder.ToTable("care_service_categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.DisplayOrder).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();

        builder.HasIndex(c => c.Code).IsUnique();

        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class CareServiceConfiguration : IEntityTypeConfiguration<CareService>
{
    public void Configure(EntityTypeBuilder<CareService> builder)
    {
        builder.ToTable("care_services");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Code).IsRequired().HasMaxLength(50);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Description).HasMaxLength(1000);
        builder.Property(s => s.IsActive).IsRequired();

        builder.HasIndex(s => s.Code).IsUnique();

        builder.HasOne(s => s.Category)
            .WithMany()
            .HasForeignKey(s => s.CareServiceCategoryId)
            // Reference data must not vanish while services still point at it.
            .OnDelete(DeleteBehavior.Restrict);

        // Npgsql stores these as text[] and int[] columns.
        builder.PrimitiveCollection(s => s.RequiredCertificationCodes)
            .HasColumnName("required_certification_codes")
            .ElementType(element => element.HasMaxLength(50));

        builder.PrimitiveCollection(s => s.ApplicableRecipientTypes)
            .HasColumnName("applicable_recipient_types");

        builder.PrimitiveCollection(s => s.ApplicablePetSpecies)
            .HasColumnName("applicable_pet_species");

        builder.Property(s => s.PricingUnit).IsRequired().HasConversion<int>();
        builder.Property(s => s.CareLocation).IsRequired().HasConversion<int>();

        builder.Ignore(s => s.IsPetCare);

        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.ToTable("favorites");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.CreatedAtUtc).IsRequired();

        // A caregiver can only be saved once by the same requester.
        builder.HasIndex(f => new { f.CareRequesterProfileId, f.CaregiverProfileId }).IsUnique();

        builder.HasOne<CaregiverProfile>()
            .WithMany()
            .HasForeignKey(f => f.CaregiverProfileId)
            // Removing a caregiver clears them from everyone's saved list.
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(f => f.DomainEvents);
    }
}
