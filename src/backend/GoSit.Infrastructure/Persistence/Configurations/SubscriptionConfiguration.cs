using GoSit.Domain.Growth;
using GoSit.Domain.Subscriptions;
using GoSit.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("subscription_plans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Audience).IsRequired().HasConversion<int>();
        builder.Property(p => p.MonthlyPrice).IsRequired().HasPrecision(10, 2);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.DisplayOrder).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();

        // The provider's Price id. Nullable: a free plan never has one, and a paid plan
        // has none until billing is configured for the environment.
        builder.Property(p => p.ProviderPriceId).HasMaxLength(255);

        builder.HasIndex(p => p.Code).IsUnique();

        builder.PrimitiveCollection(p => p.Features)
            .HasColumnName("features")
            .ElementType(element => element.HasMaxLength(50));

        builder.Ignore(p => p.DomainEvents);
    }
}

public sealed class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.ToTable("user_subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status).IsRequired().HasConversion<int>();
        builder.Property(s => s.CurrentPeriodStartUtc).IsRequired();
        builder.Property(s => s.CurrentPeriodEndUtc).IsRequired();
        builder.Property(s => s.CancelAtPeriodEnd).IsRequired();
        builder.Property(s => s.ProviderSubscriptionId).HasMaxLength(255);
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.Version).IsRequired().IsConcurrencyToken();

        builder.HasOne(s => s.Plan)
            .WithMany()
            .HasForeignKey(s => s.SubscriptionPlanId)
            // Reference data must not vanish while subscriptions point at it.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Every entitlement check starts here, so the lookup has to be indexed.
        builder.HasIndex(s => new { s.UserId, s.Status });

        // The sweep that closes out expired and past-due subscriptions.
        builder.HasIndex(s => new { s.Status, s.CurrentPeriodEndUtc });

        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class ReferralConfiguration : IEntityTypeConfiguration<Referral>
{
    public void Configure(EntityTypeBuilder<Referral> builder)
    {
        builder.ToTable("referrals");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Code).IsRequired().HasMaxLength(32);
        builder.Property(r => r.Status).IsRequired().HasConversion<int>();
        builder.Property(r => r.RewardAmount).HasPrecision(10, 2);
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.Version).IsRequired().IsConcurrencyToken();

        // Someone can be referred once, ever. The index is what actually enforces it.
        builder.HasIndex(r => r.RefereeUserId).IsUnique();

        builder.HasIndex(r => new { r.ReferrerUserId, r.Status });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.ReferrerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.RefereeUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        builder.ToTable("promo_codes");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).IsRequired().HasMaxLength(32);
        builder.Property(c => c.Description).IsRequired().HasMaxLength(200);
        builder.Property(c => c.DiscountType).IsRequired().HasConversion<int>();
        builder.Property(c => c.Value).IsRequired().HasPrecision(10, 2);
        builder.Property(c => c.Audience).HasConversion<int?>();
        builder.Property(c => c.ValidFromUtc).IsRequired();
        builder.Property(c => c.RedemptionCount).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();

        // Guards the redemption cap against a concurrent double-spend.
        builder.Property(c => c.Version).IsRequired().IsConcurrencyToken();

        builder.HasIndex(c => c.Code).IsUnique();

        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class PromoRedemptionConfiguration : IEntityTypeConfiguration<PromoRedemption>
{
    public void Configure(EntityTypeBuilder<PromoRedemption> builder)
    {
        builder.ToTable("promo_redemptions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.DiscountAmount).IsRequired().HasPrecision(10, 2);
        builder.Property(r => r.RedeemedAtUtc).IsRequired();

        // One use per account, enforced by the database rather than by a check-then-act.
        builder.HasIndex(r => new { r.PromoCodeId, r.UserId }).IsUnique();

        builder.HasOne<PromoCode>()
            .WithMany()
            .HasForeignKey(r => r.PromoCodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(r => r.DomainEvents);
    }
}
