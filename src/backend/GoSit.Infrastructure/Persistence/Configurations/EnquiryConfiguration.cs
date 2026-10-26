using GoSit.Domain.Enquiries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class EnquiryConfiguration : IEntityTypeConfiguration<Enquiry>
{
    public void Configure(EntityTypeBuilder<Enquiry> builder)
    {
        builder.ToTable("enquiries");

        builder.HasKey(e => e.Id);

        // Personal information from a member of the public. Never logged.
        builder.Property(e => e.Name).IsRequired().HasMaxLength(Enquiry.MaxNameLength);
        builder.Property(e => e.Email).IsRequired().HasMaxLength(Enquiry.MaxEmailLength);
        builder.Property(e => e.Message).IsRequired().HasMaxLength(Enquiry.MaxMessageLength);

        builder.Property(e => e.Kind).IsRequired().HasConversion<int>();
        builder.Property(e => e.Status).IsRequired().HasConversion<int>();
        builder.Property(e => e.Source).IsRequired().HasMaxLength(Enquiry.MaxSourceLength);

        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.ConsentedAtUtc).IsRequired();
        builder.Property(e => e.Version).IsRequired().IsConcurrencyToken();

        // Deliberately no foreign key to a user: the sender has no account, and the staff
        // member who handled it must not be a reason an account cannot be closed.
        builder.Property(e => e.HandledByUserId);

        // The staff queue: new first, oldest first within it.
        builder.HasIndex(e => new { e.Status, e.CreatedAtUtc });

        builder.Ignore(e => e.DomainEvents);
    }
}
