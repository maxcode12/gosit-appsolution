using GoSit.Domain.Common.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared column mapping for the owned <see cref="Address"/> value object, so the
/// requester and caregiver tables describe an address identically.
/// </summary>
internal static class AddressConfiguration
{
    public static void Configure<TOwner>(OwnedNavigationBuilder<TOwner, Address> builder)
        where TOwner : class
    {
        builder.Property(a => a.Line1).HasColumnName("address_line1").HasMaxLength(200).IsRequired();
        builder.Property(a => a.Line2).HasColumnName("address_line2").HasMaxLength(200);
        builder.Property(a => a.City).HasColumnName("address_city").HasMaxLength(100).IsRequired();
        builder.Property(a => a.Province).HasColumnName("address_province").HasMaxLength(2).IsRequired();

        // Stored without the inner space, so lookups by postal code are exact matches.
        builder.Property(a => a.PostalCode).HasColumnName("address_postal_code").HasMaxLength(6).IsRequired();
        builder.Property(a => a.Country).HasColumnName("address_country").HasMaxLength(2).IsRequired();

        builder.Property(a => a.Latitude).HasColumnName("address_latitude");
        builder.Property(a => a.Longitude).HasColumnName("address_longitude");

        // Search filters by city and by the postal code prefix, so both are indexed.
        builder.HasIndex(a => a.City);
        builder.HasIndex(a => a.PostalCode);
    }
}
