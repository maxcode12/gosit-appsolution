using GoSit.Domain.CareRequesters;
using GoSit.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class CareRequesterProfileConfiguration : IEntityTypeConfiguration<CareRequesterProfile>
{
    public void Configure(EntityTypeBuilder<CareRequesterProfile> builder)
    {
        builder.ToTable("care_requester_profiles");

        builder.HasKey(p => p.Id);

        // One profile per account, and the lookup path for every "my profile" request.
        builder.HasIndex(p => p.UserId).IsUnique();

        // A profile must not outlive the account it belongs to. Without this, deleting a
        // user leaves the care recipients and their care needs orphaned in the database.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.PhoneNumber).IsRequired().HasMaxLength(20);
        builder.Property(p => p.AboutMe).HasMaxLength(2000);
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc);

        builder.OwnsOne(p => p.Address, AddressConfiguration.Configure);

        builder.HasMany(p => p.CareRecipients)
            .WithOne()
            .HasForeignKey(r => r.CareRequesterProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.EmergencyContacts)
            .WithOne()
            .HasForeignKey(c => c.CareRequesterProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.CareRecipients).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.EmergencyContacts).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);
    }
}
