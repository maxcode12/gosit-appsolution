using GoSit.Application.Abstractions;
using GoSit.Application.Features.Authentication.AccountRecovery;
using GoSit.Infrastructure.Email;
using GoSit.Infrastructure.Documents;
using GoSit.Infrastructure.Geocoding;
using GoSit.Infrastructure.Messaging;
using GoSit.Infrastructure.Payments;
using GoSit.Infrastructure.Persistence;
using GoSit.Infrastructure.Persistence.Repositories;
using GoSit.Infrastructure.Registration;
using GoSit.Infrastructure.Scheduling;
using GoSit.Infrastructure.Screening;
using GoSit.Infrastructure.Security;
using GoSit.Infrastructure.Time;
using GoSit.Infrastructure.Video;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GoSit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var postgres = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddDbContext<GoSitDbContext>(options => options.UseNpgsql(postgres));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<GoSitDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICareRequesterProfileRepository, CareRequesterProfileRepository>();
        services.AddScoped<ICaregiverProfileRepository, CaregiverProfileRepository>();
        services.AddScoped<ICertificationRepository, CertificationRepository>();
        services.AddScoped<ICaregiverSearchRepository, CaregiverSearchRepository>();
        services.AddScoped<ICaregiverInsightsRepository, CaregiverInsightsRepository>();
        services.AddScoped<ICaregiverExposureRepository, CaregiverExposureRepository>();
        services.AddScoped<IFavoriteRepository, FavoriteRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingLookupRepository, BookingLookupRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IProfileDirectory, ProfileDirectory>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPayoutRepository, PayoutRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IVerificationRepository, VerificationRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IEnquiryRepository, EnquiryRepository>();
        services.Configure<EnquiryOptions>(configuration.GetSection(EnquiryOptions.SectionName));
        services.AddScoped<IEnquiryAlert, EmailEnquiryAlert>();
        services.AddScoped<IAdminLookupRepository, AdminLookupRepository>();
        services.AddScoped<ICertificationReviewRepository, CertificationReviewRepository>();
        services.AddScoped<IDisputeRepository, DisputeRepository>();
        services.AddScoped<IMessageModerationRepository, MessageModerationRepository>();
        services.AddScoped<IInsuranceReviewRepository, InsuranceReviewRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IGrowthRepository, GrowthRepository>();
        services.AddScoped<IInterviewRepository, InterviewRepository>();
        // Certn when its key is configured, otherwise every case waits for a staff decision.
        services.Configure<CertnOptions>(configuration.GetSection(CertnOptions.SectionName));
        services.AddScoped<UnconfiguredBackgroundCheckProvider>();
        services.AddHttpClient<CertnBackgroundCheckProvider>(CertnHttpClientRegistration.Configure);
        services.AddScoped<IBackgroundCheckProvider>(sp =>
            sp.GetRequiredService<IOptions<CertnOptions>>().Value.IsConfigured
                ? sp.GetRequiredService<CertnBackgroundCheckProvider>()
                : sp.GetRequiredService<UnconfiguredBackgroundCheckProvider>());

        // Registered as itself as well, so a test can drive one deterministic sweep.
        services.AddSingleton<ReviewPublicationSweeper>();
        services.AddSingleton<SubscriptionSweeper>();
        services.AddHostedService(sp => sp.GetRequiredService<SubscriptionSweeper>());
        services.AddHostedService(sp => sp.GetRequiredService<ReviewPublicationSweeper>());
        services.AddSingleton<PayoutSweeper>();
        services.AddHostedService(sp => sp.GetRequiredService<PayoutSweeper>());
        services.AddSingleton<CertificationExpirySweeper>();
        services.AddHostedService(sp => sp.GetRequiredService<CertificationExpirySweeper>());

        services.AddSingleton<InsuranceExpirySweeper>();
        services.AddHostedService(sp => sp.GetRequiredService<InsuranceExpirySweeper>());

        services.Configure<ExposureRetentionOptions>(
            configuration.GetSection(ExposureRetentionOptions.SectionName));
        services.AddSingleton<ExposureRetentionSweeper>();
        services.AddHostedService(sp => sp.GetRequiredService<ExposureRetentionSweeper>());

        services.AddDocumentStorage(configuration);
        services.AddVideoInterviews(configuration);
        services.AddGeocoding(configuration);
        services.AddSingleton<GeocodingBackfillSweeper>();
        services.AddHostedService(sp => sp.GetRequiredService<GeocodingBackfillSweeper>());

        services.AddEmail(configuration);
        services.AddScoped<IAccountRecoveryNotifier, AccountRecoveryNotifier>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<NotificationEmailDispatcher>();
        services.AddHostedService(sp => sp.GetRequiredService<NotificationEmailDispatcher>());
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<ReferenceDataSeeder>();

        services.Configure<AdministratorSeedOptions>(
            configuration.GetSection(AdministratorSeedOptions.SectionName));
        services.AddScoped<AdministratorSeeder>();

        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.AddSingleton<IGoogleIdTokenVerifier, GoogleIdTokenVerifier>();

        services.Configure<RegistrationOptions>(
            configuration.GetSection(RegistrationOptions.SectionName));
        services.AddSingleton<IRegistrationPolicy, ConfiguredRegistrationPolicy>();

        // The outbox drains to a logger until an Azure Service Bus namespace exists.
        services.AddScoped<IIntegrationEventPublisher, LoggingIntegrationEventPublisher>();
        services.AddHostedService<OutboxPublisher>();

        // Registered as itself as well as a hosted service, so the sweep can be triggered
        // directly rather than only on its poll interval.
        services.AddSingleton<BookingSweeper>();
        services.AddHostedService(sp => sp.GetRequiredService<BookingSweeper>());

        var redis = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
        }

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // Fail at startup rather than on the first login attempt.
            .ValidateOnStart();

        services.AddStripePayments(configuration);

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<Observability.BusinessMetrics>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;
    }
}
