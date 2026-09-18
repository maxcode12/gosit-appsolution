using GoSit.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoSit.Infrastructure.Geocoding;

public static class GeocodingServiceRegistration
{
    public static IServiceCollection AddGeocoding(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AzureMapsOptions>()
            .Bind(configuration.GetSection(AzureMapsOptions.SectionName))
            .ValidateDataAnnotations();

        var maps = configuration.GetSection(AzureMapsOptions.SectionName).Get<AzureMapsOptions>()
            ?? new AzureMapsOptions();

        if (maps.IsConfigured)
        {
            // A typed client, so the handler is pooled rather than a new socket per
            // save — this runs on every profile update.
            services.AddHttpClient<IGeocoder, AzureMapsGeocoder>(client =>
            {
                client.BaseAddress = new Uri(maps.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(maps.TimeoutSeconds);
            });
        }
        else
        {
            services.AddSingleton<IGeocoder, UnconfiguredGeocoder>();
        }

        return services;
    }
}
