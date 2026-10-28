using GoSit.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);

        builder.Property(u => u.NormalizedEmail).IsRequired().HasMaxLength(256);

        // One account per email address; also the lookup path for login.
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();

        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);

        // Google's stable id for the person. Filtered unique: most accounts have none.
        builder.Property(u => u.GoogleSubject).HasMaxLength(64);
        builder.HasIndex(u => u.GoogleSubject)
            .IsUnique()
            .HasFilter("\"GoogleSubject\" IS NOT NULL");

        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(100);

        builder.Property(u => u.Role).IsRequired().HasConversion<int>();
        builder.Property(u => u.Status).IsRequired().HasConversion<int>();

        builder.Property(u => u.EmailVerified).IsRequired();

        // The billing provider's Customer id, so an account keeps one customer across
        // subscriptions rather than accumulating one per purchase.
        builder.Property(u => u.ProviderCustomerId).HasMaxLength(255);
        builder.Property(u => u.CreatedAtUtc).IsRequired();
        builder.Property(u => u.LastLoginAtUtc);

        builder.Property(u => u.FailedLoginAttempts).IsRequired();
        builder.Property(u => u.LockedOutUntilUtc);

        builder.Property(u => u.ReferralCode).HasMaxLength(32);

        // Referral codes are looked up on redemption and must be unique. Filtered,
        // because a code is assigned lazily and most rows have none.
        builder.HasIndex(u => u.ReferralCode)
            .IsUnique()
            .HasFilter("\"ReferralCode\" IS NOT NULL");

        // No optimistic concurrency token here. PostgreSQL's xmin is not usable as one on
        // this EF/Npgsql version: it is not projected on read, so every UPDATE compares
        // against zero and matches no rows. Concurrent writes to a User are also benign —
        // issuing two refresh tokens at once is fine and revocation is idempotent.
        // Booking, where interleaved state transitions genuinely conflict, will carry an
        // explicit version column instead.

        builder.HasMany(u => u.RefreshTokens)
            .WithOne()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.RefreshTokens)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Emailed verification and password-reset tokens. Cascade-deleted with the
        // account, like refresh tokens — a deleted account must not leave live keys to
        // itself behind.
        builder.HasMany(u => u.Tokens)
            .WithOne()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.Tokens)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(u => u.DomainEvents);
    }
}

/// <summary>
/// Emailed tokens. Only the hash is stored — see <see cref="UserToken"/>.
/// </summary>
public sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.ToTable("user_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.Purpose).IsRequired();

        // Hex-encoded SHA-256: always 64 characters.
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);

        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.ExpiresAtUtc).IsRequired();
        builder.Property(t => t.ConsumedAtUtc);

        // Redemption looks a token up by hash and purpose, and it is the hot path on
        // every click of an emailed link.
        builder.HasIndex(t => new { t.Purpose, t.TokenHash });

        builder.Ignore(t => t.DomainEvents);
    }
}
