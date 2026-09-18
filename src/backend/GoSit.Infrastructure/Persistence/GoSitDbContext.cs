using GoSit.Application.Abstractions;
using GoSit.Domain.Audit;
using GoSit.Domain.Bookings;
using GoSit.Domain.CareRecipients;
using GoSit.Domain.CareRequesters;
using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Common;
using GoSit.Domain.Messaging;
using GoSit.Domain.Notifications;
using GoSit.Domain.Payments;
using GoSit.Domain.Payouts;
using GoSit.Domain.Growth;
using GoSit.Domain.Interviews;
using GoSit.Domain.Reviews;
using GoSit.Domain.Subscriptions;
using GoSit.Domain.TrustAndSafety;
using GoSit.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence;

/// <summary>
/// Primary EF Core context for the GoSit modular monolith.
/// Entity configurations are added per feature phase via <see cref="IEntityTypeConfiguration{TEntity}"/>
/// classes discovered from this assembly.
/// </summary>
public sealed class GoSitDbContext(
    DbContextOptions<GoSitDbContext> options,
    Observability.BusinessMetrics? metrics = null)
    : DbContext(options), IUnitOfWork
{
    /// <summary>
    /// Optional metrics sink. Supplied by the container when the app runs; null under
    /// the EF design-time tools, which build the context without one.
    /// </summary>
    private Observability.BusinessMetrics? Metrics => metrics;

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UserToken> UserTokens => Set<UserToken>();

    public DbSet<CareRequesterProfile> CareRequesterProfiles => Set<CareRequesterProfile>();

    public DbSet<CareRecipient> CareRecipients => Set<CareRecipient>();

    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();

    public DbSet<CaregiverProfile> CaregiverProfiles => Set<CaregiverProfile>();

    public DbSet<CaregiverExposure> CaregiverExposures => Set<CaregiverExposure>();

    public DbSet<Certification> Certifications => Set<Certification>();

    public DbSet<CaregiverCertification> CaregiverCertifications => Set<CaregiverCertification>();

    public DbSet<CaregiverInsurancePolicy> CaregiverInsurancePolicies => Set<CaregiverInsurancePolicy>();

    public DbSet<CareServiceCategory> CareServiceCategories => Set<CareServiceCategory>();

    public DbSet<CareService> CareServices => Set<CareService>();

    public DbSet<Favorite> Favorites => Set<Favorite>();

    public DbSet<CareBooking> CareBookings => Set<CareBooking>();

    public DbSet<BookingRecipient> BookingRecipients => Set<BookingRecipient>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Refund> Refunds => Set<Refund>();

    public DbSet<Payout> Payouts => Set<Payout>();

    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();

    public DbSet<ReviewPair> ReviewPairs => Set<ReviewPair>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<VerificationCase> VerificationCases => Set<VerificationCase>();

    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();

    public DbSet<Referral> Referrals => Set<Referral>();

    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();

    public DbSet<PromoRedemption> PromoRedemptions => Set<PromoRedemption>();

    public DbSet<Interview> Interviews => Set<Interview>();

    public DbSet<InterviewRecording> InterviewRecordings => Set<InterviewRecording>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GoSitDbContext).Assembly);

        // Every aggregate assigns its own Guid identity (see Entity.Id), so no key is
        // store-generated. Without this, EF sees a non-default key on an entity newly
        // added to a tracked navigation and infers the row already exists — emitting an
        // UPDATE that matches nothing instead of an INSERT.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var key = entityType.FindPrimaryKey();
            if (key?.Properties is [{ ClrType: var clrType } property] && clrType == typeof(Guid))
            {
                property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedAtUtc = utcNow;
            }
        }

        // Outbox rows are written inside this same save, so business state and the
        // intent to publish commit together or not at all.
        this.CaptureDomainEvents(utcNow, Metrics);

        return base.SaveChangesAsync(cancellationToken);
    }
}
