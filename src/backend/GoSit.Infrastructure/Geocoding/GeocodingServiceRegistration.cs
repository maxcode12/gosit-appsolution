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
        // Free and always available: the postal-area data ships inside the application.
        // It answers "roughly where is this postal code" at search time and is never written
        // onto an address. A coordinate stored on an address is read as "this is the house":
        // arrival check-in compares a caregiver's phone against it. A neighbourhood's centre
        // point, saved there, would read as a street address and be kilometres wrong.
        //
        // Built by hand rather than resolved: the class has a second constructor for tests,
        // and the container would pick it (an empty list of strings is "resolvable") and
        // hand back a directory that knows no areas at all.
        services.AddSingleton<IPostalAreaDirectory>(_ => new PostalAreaDirectory());

        services.AddOptions<AzureMapsOptions>()
            .Bind(configuration.GetSection(AzureMapsOptions.SectionName))
            .ValidateDataAnnotations();

        var maps = configuration.GetSection(AzureMapsOptions.SectionName).Get<AzureMapsOptions>()
            ?? new AzureMapsOptions();

        services.AddOptions<GoogleMapsOptions>()
            .Bind(configuration.GetSection(GoogleMapsOptions.SectionName))
            .ValidateDataAnnotations();

        var google = configuration.GetSection(GoogleMapsOptions.SectionName).Get<GoogleMapsOptions>()
            ?? new GoogleMapsOptions();

        if (google.IsConfigured)
        {
            // Google puts the key in the URL, so the client's own request logging — which
            // prints the full URI at Information level — is removed for this client.
            services.AddHttpClient<IGeocoder, GoogleMapsGeocoder>(client =>
                {
                    client.BaseAddress = new Uri(google.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(google.TimeoutSeconds);
                })
                .RemoveAllLoggers();
        }
        else if (maps.IsConfigured)
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
