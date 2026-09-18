using System.Reflection;
using FluentValidation;
using GoSit.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace GoSit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<Features.Bookings.BookingProjector>();
        services.AddScoped<Features.Payments.HandleWebhook.PayoutFactory>();
        services.AddScoped<Features.Reviews.RatingRecalculator>();
        services.AddScoped<Features.Subscriptions.EntitlementService>();
        services.AddScoped<Features.Subscriptions.SubscriptionWebhookApplier>();
        services.AddScoped<Features.Payouts.PayoutAccountWebhookApplier>();
        services.AddScoped<Common.AddressResolver>();
        services.AddScoped<Features.Growth.ReferralQualifier>();
        services.AddScoped<Features.Bookings.BookingCompletionEffects>();
        services.AddScoped<Features.Bookings.BookingNotifier>();
        services.AddScoped<Features.Messaging.SendMessage.MessageDelivery>();
        services.AddScoped<Features.Messaging.Queries.MessagingParticipantResolver>();
        services.AddScoped<Features.Interviews.InterviewAccess>();
        services.AddScoped<Features.Interviews.InterviewPresenter>();
        services.AddScoped<Features.Interviews.InterviewNotifier>();
        services.AddFeatureHandlers(assembly);

        return services;
    }

    /// <summary>
    /// Registers every <see cref="ICommandHandler{TCommand,TResponse}"/> and
    /// <see cref="IQueryHandler{TQuery,TResponse}"/> in the assembly, so new feature
    /// slices become available without editing this file.
    /// </summary>
    private static void AddFeatureHandlers(this IServiceCollection services, Assembly assembly)
    {
        var openHandlerInterfaces = new[] { typeof(ICommandHandler<,>), typeof(IQueryHandler<,>) };

        var registrations =
            from type in assembly.GetTypes()
            where type is { IsAbstract: false, IsInterface: false }
            from @interface in type.GetInterfaces()
            where @interface.IsGenericType
                && openHandlerInterfaces.Contains(@interface.GetGenericTypeDefinition())
            select (Service: @interface, Implementation: type);

        foreach (var (service, implementation) in registrations)
        {
            services.AddScoped(service, implementation);
        }
    }
}
