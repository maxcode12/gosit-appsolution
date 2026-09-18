using GoSit.Domain.CareRequesters;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Messaging;
using GoSit.Domain.Notifications;
using GoSit.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.LastMessageAtUtc);
        builder.Property(c => c.CareRequesterLastReadAtUtc);
        builder.Property(c => c.CaregiverLastReadAtUtc);

        // One thread per pair, so repeat bookings share a single history.
        builder.HasIndex(c => new { c.CareRequesterProfileId, c.CaregiverProfileId }).IsUnique();

        // Both inboxes sort by most recent activity.
        builder.HasIndex(c => new { c.CareRequesterProfileId, c.LastMessageAtUtc });
        builder.HasIndex(c => new { c.CaregiverProfileId, c.LastMessageAtUtc });

        builder.HasOne<CareRequesterProfile>()
            .WithMany()
            .HasForeignKey(c => c.CareRequesterProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<CaregiverProfile>()
            .WithMany()
            .HasForeignKey(c => c.CaregiverProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Messages).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Body).IsRequired().HasMaxLength(Message.MaxBodyLength);
        builder.Property(m => m.SentAtUtc).IsRequired();
        builder.Property(m => m.SentByCaregiver).IsRequired();
        builder.Property(m => m.FlaggedForReview).IsRequired();
        builder.Property(m => m.ModerationOutcome).HasConversion<int?>();
        builder.Property(m => m.ModeratedByUserId);
        builder.Property(m => m.ModeratedAtUtc);

        // Reading a thread, newest last.
        builder.HasIndex(m => new { m.ConversationId, m.SentAtUtc });

        // Supports the moderation queue in Phase 7, kept small by the filter.
        builder.HasIndex(m => m.SentAtUtc)
            .HasFilter("\"FlaggedForReview\" = true");

        builder.Ignore(m => m.AwaitsModeration);
        builder.Ignore(m => m.DomainEvents);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type).IsRequired().HasConversion<int>();
        builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Body).HasMaxLength(500);
        builder.Property(n => n.RelatedEntityType).HasMaxLength(100);
        builder.Property(n => n.CreatedAtUtc).IsRequired();
        builder.Property(n => n.ReadAtUtc);

        // Email delivery bookkeeping.
        builder.Property(n => n.EmailSentAtUtc);
        builder.Property(n => n.EmailSuppressed).IsRequired();
        builder.Property(n => n.EmailAttemptCount).IsRequired();
        builder.Property(n => n.LastEmailAttemptAtUtc);

        // The dispatcher's query, every thirty seconds. Partial, so the index stays
        // small: the overwhelming majority of notifications are already delivered.
        builder.HasIndex(n => n.CreatedAtUtc)
            .HasDatabaseName("IX_notifications_pending_email")
            .HasFilter("\"EmailSentAtUtc\" IS NULL AND NOT \"EmailSuppressed\"");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The notification list: newest first for one account.
        builder.HasIndex(n => new { n.UserId, n.CreatedAtUtc });

        // The unread badge, filtered so the index stays small as history accumulates.
        builder.HasIndex(n => n.UserId)
            .HasFilter("\"ReadAtUtc\" IS NULL")
            .HasDatabaseName("IX_notifications_UserId_Unread");

        builder.Ignore(n => n.DomainEvents);
    }
}
