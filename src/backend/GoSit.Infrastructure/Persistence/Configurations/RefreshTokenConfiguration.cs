using GoSit.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);

        // Hex-encoded SHA-256 is always 64 characters.
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);

        // Refresh and logout both look tokens up by hash, so this must be indexed.
        // Unique because two accounts sharing a token hash would be a collision or a bug.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.ExpiresAtUtc).IsRequired();
        builder.Property(t => t.RevokedAtUtc);
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);

        // Supports cleanup of expired tokens by a background job.
        builder.HasIndex(t => t.ExpiresAtUtc);

        builder.Ignore(t => t.DomainEvents);
    }
}
