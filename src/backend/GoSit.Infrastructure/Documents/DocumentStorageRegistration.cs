using GoSit.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoSit.Infrastructure.Documents;

public static class DocumentStorageRegistration
{
    /// <summary>
    /// Real storage when a connection string is configured, and a stand-in that refuses
    /// when it is not.
    ///
    /// The refusing stand-in is the point. Every other provider in GoSit degrades quietly
    /// when unconfigured, because the cost is a feature nobody notices — distance ranking,
    /// a notification email. Storage cannot: silently dropping an upload would leave a
    /// caregiver believing their record check is on file and staff approving a
    /// certification whose evidence never existed.
    /// </summary>
    public static IServiceCollection AddDocumentStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(DocumentStorageOptions.SectionName);

        services.Configure<DocumentStorageOptions>(section);

        var options = section.Get<DocumentStorageOptions>() ?? new DocumentStorageOptions();

        if (options.IsConfigured)
        {
            services.AddSingleton<IDocumentStore, BlobDocumentStore>();
        }
        else
        {
            services.AddSingleton<IDocumentStore, UnconfiguredDocumentStore>();
        }

        return services;
    }
}
