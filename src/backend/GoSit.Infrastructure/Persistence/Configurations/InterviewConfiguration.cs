using GoSit.Domain.CareRequesters;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Interviews;
using GoSit.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoSit.Infrastructure.Persistence.Configurations;

public sealed class InterviewConfiguration : IEntityTypeConfiguration<Interview>
{
    public void Configure(EntityTypeBuilder<Interview> builder)
    {
        builder.ToTable("interviews");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ScheduledStartUtc).IsRequired();
        builder.Property(i => i.DurationMinutes).IsRequired();
        builder.Property(i => i.Status).IsRequired().HasConversion<int>();
        builder.Property(i => i.RequestedAtUtc).IsRequired();
        builder.Property(i => i.RespondedAtUtc);
        builder.Property(i => i.CancelledAtUtc);
        builder.Property(i => i.CancelledByCaregiver);
        builder.Property(i => i.CareRequesterConsentedAtUtc);
        builder.Property(i => i.CaregiverConsentedAtUtc);
        builder.Property(i => i.CareRequesterJoinedAtUtc);
        builder.Property(i => i.CaregiverJoinedAtUtc);
        builder.Property(i => i.EndedAtUtc);
        builder.Property(i => i.RecordingLeaseUntilUtc);
        builder.Property(i => i.RecordingsOnHold).IsRequired();
        builder.Property(i => i.RecordingHoldReason).HasMaxLength(InterviewPolicy.MaxHoldReasonLength);
        builder.Property(i => i.RecordingHeldByUserId);
        builder.Property(i => i.RecordingHeldAtUtc);

        // Each side's list of interviews, newest first.
        builder.HasIndex(i => new { i.CareRequesterProfileId, i.ScheduledStartUtc });
        builder.HasIndex(i => new { i.CaregiverProfileId, i.ScheduledStartUtc });

        /*
         * Restrict, not cascade. An interview can hold recordings kept as evidence, and a
         * profile or conversation being removed must not silently take them with it — the
         * recordings have to be deleted from storage, on purpose, first.
         */
        builder.HasOne<CareRequesterProfile>()
            .WithMany()
            .HasForeignKey(i => i.CareRequesterProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CaregiverProfile>()
            .WithMany()
            .HasForeignKey(i => i.CaregiverProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(i => i.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Recordings)
            .WithOne()
            .HasForeignKey(r => r.InterviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Recordings).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(i => i.ScheduledEndUtc);
        builder.Ignore(i => i.JoinOpensAtUtc);
        builder.Ignore(i => i.JoinClosesAtUtc);
        builder.Ignore(i => i.WasJoined);
        builder.Ignore(i => i.RoomName);
        builder.Ignore(i => i.DomainEvents);
    }
}

public sealed class InterviewRecordingConfiguration : IEntityTypeConfiguration<InterviewRecording>
{
    public void Configure(EntityTypeBuilder<InterviewRecording> builder)
    {
        builder.ToTable("interview_recordings");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ProviderRecordingId).IsRequired().HasMaxLength(100);
        builder.Property(r => r.StorageKey).HasMaxLength(300);
        builder.Property(r => r.Status).IsRequired().HasConversion<int>();
        builder.Property(r => r.StartedAtUtc).IsRequired();
        builder.Property(r => r.EndedAtUtc);
        builder.Property(r => r.DeleteAfterUtc);
        builder.Property(r => r.DeletedAtUtc);

        // Webhooks find a recording by the video service's id.
        builder.HasIndex(r => r.ProviderRecordingId).IsUnique();

        // The retention sweep, kept small by skipping what is already gone.
        builder.HasIndex(r => r.DeleteAfterUtc)
            .HasFilter("\"StorageKey\" IS NOT NULL");

        builder.Ignore(r => r.IsWatchable);
        builder.Ignore(r => r.DomainEvents);
    }
}
