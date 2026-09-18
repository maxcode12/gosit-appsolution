using GoSit.Application.Abstractions;
using GoSit.Domain.Payouts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoSit.Infrastructure.Payments;

public static class PaymentServiceRegistration
{
    public static IServiceCollection AddStripePayments(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<StripeOptions>()
            .Bind(configuration.GetSection(StripeOptions.SectionName))
            .ValidateDataAnnotations();

        var stripe = configuration.GetSection(StripeOptions.SectionName).Get<StripeOptions>()
            ?? new StripeOptions();

        // With no secret key configured — local development and the integration tests —
        // the simulated provider stands in. Webhook verification is NOT simulated: the
        // real Stripe parser is always used, so the security boundary is the same code
        // in every environment.
        if (stripe.IsConfigured)
        {
            services.AddScoped<IPaymentProvider, StripePaymentProvider>();
            services.AddScoped<ISubscriptionBillingProvider, StripeSubscriptionBillingProvider>();
            services.AddScoped<IPayoutAccountProvider, StripePayoutAccountProvider>();
        }
        else
        {
            services.AddScoped<IPaymentProvider, SimulatedPaymentProvider>();
            services.AddScoped<ISubscriptionBillingProvider, SimulatedSubscriptionBillingProvider>();
            services.AddScoped<IPayoutAccountProvider, SimulatedPayoutAccountProvider>();
        }

        services.AddScoped<IPaymentWebhookParser, StripeWebhookParser>();

        services.AddOptions<WebAppOptions>().Bind(configuration.GetSection(WebAppOptions.SectionName));
        services.AddScoped<IBillingReturnUrls, BillingReturnUrls>();
        services.AddScoped<IPayoutReturnUrls, PayoutReturnUrls>();

        // Both rates are open business decisions; see PlatformFeePolicy. The reduced one
        // applies to caregivers whose plan includes REDUCED_COMMISSION.
        var commissionRate = configuration.GetValue<decimal?>("Payments:CommissionRate")
            ?? PlatformFeePolicy.DefaultCommissionRate;

        var reducedCommissionRate =
            configuration.GetValue<decimal?>("Payments:ReducedCommissionRate")
            ?? PlatformFeePolicy.DefaultReducedCommissionRate;

        services.AddSingleton(new PlatformFeePolicy(commissionRate, reducedCommissionRate));

        return services;
    }
}
