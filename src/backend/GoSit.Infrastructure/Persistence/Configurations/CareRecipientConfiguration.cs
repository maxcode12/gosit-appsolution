using GoSit.Domain.CareRecipients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class CareRecipientConfiguration : IEntityTypeConfiguration<CareRecipient>
{
    public void Configure(EntityTypeBuilder<CareRecipient> builder)
    {
        builder.ToTable("care_recipients");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(r => r.LastName).HasMaxLength(100);
        builder.Property(r => r.DateOfBirth).IsRequired();
        builder.Property(r => r.RecipientType).IsRequired().HasConversion<int>();
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.UpdatedAtUtc);

        builder.HasIndex(r => r.CareRequesterProfileId);

        // Care needs live in their own table rather than as extra columns here: they are
        // markedly more sensitive than a name and date of birth, so keeping them separate
        // lets queries that do not need them avoid loading them at all.
        builder.OwnsOne(r => r.CareNeeds, needs =>
        {
            needs.ToTable("care_needs_profiles");

            needs.Property(n => n.MobilityNeeds).HasMaxLength(2000);
            needs.Property(n => n.CommunicationStyle).HasMaxLength(2000);
            needs.Property(n => n.SensoryNeeds).HasMaxLength(2000);
            needs.Property(n => n.BehaviourSupportNotes).HasMaxLength(2000);
            needs.Property(n => n.DietaryRestrictions).HasMaxLength(2000);
            needs.Property(n => n.Allergies).HasMaxLength(2000);
            needs.Property(n => n.RoutinePreferences).HasMaxLength(2000);
            needs.Property(n => n.Triggers).HasMaxLength(2000);
            needs.Property(n => n.CalmingStrategies).HasMaxLength(2000);
            needs.Property(n => n.EmergencyInstructions).HasMaxLength(2000);

            needs.Property(n => n.ToiletingAssistance).IsRequired();
            needs.Property(n => n.FeedingAssistance).IsRequired();
            needs.Property(n => n.UpdatedAtUtc);

            // A primitive collection; Npgsql stores it as a text[] column.
            needs.PrimitiveCollection(n => n.RequiredSkills)
                .HasColumnName("required_skills")
                .ElementType(element => element.HasMaxLength(50));

            needs.Ignore(n => n.DomainEvents);
        });

        builder.Navigation(r => r.CareNeeds).IsRequired();

        builder.Ignore(r => r.DomainEvents);
    }
}
