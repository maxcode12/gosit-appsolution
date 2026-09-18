using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using GoSit.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoSit.Infrastructure.Documents;

/// <summary>
/// Blob Storage configuration.
///
/// The connection string is a secret in every deployed environment — Key Vault there,
/// user secrets locally. Azurite's well-known development string is the one exception,
/// and it is only ever set in appsettings.Development.json.
/// </summary>
public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// One container for certification evidence, kept apart from anything else GoSit
    /// might store later. A single mixed container makes "who can read this" a per-blob
    /// question, and that is the kind of rule that eventually gets one blob wrong.
    /// </summary>
    public string ContainerName { get; set; } = "certification-documents";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}

/// <summary>
/// Certification evidence in Azure Blob Storage.
///
/// The container is created private and never made public. Azure's default for a new
/// container is already private, and this passes <see cref="PublicAccessType.None"/>
/// explicitly anyway — the default is a thing that can change, and what is in here is a
/// criminal record check.
/// </summary>
public sealed class BlobDocumentStore : IDocumentStore
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<BlobDocumentStore> _logger;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private bool _initialised;

    /// <summary>
    /// The storage API version to speak.
    ///
    /// Pinned rather than left to the SDK's default, for two reasons. The SDK negotiates
    /// whatever its build is newest for, so an unrelated package upgrade silently changes
    /// the wire protocol — and Azurite, which the integration tests and local development
    /// both run on, refuses a version it does not recognise. Pinning makes the upgrade a
    /// deliberate edit rather than a surprise in someone's CI run.
    /// </summary>
    private const BlobClientOptions.ServiceVersion ApiVersion =
        BlobClientOptions.ServiceVersion.V2025_11_05;

    public BlobDocumentStore(
        IOptions<DocumentStorageOptions> options,
        ILogger<BlobDocumentStore> logger)
    {
        var configured = options.Value;

        _container = new BlobServiceClient(
                configured.ConnectionString, new BlobClientOptions(ApiVersion))
            .GetBlobContainerClient(configured.ContainerName);

        _logger = logger;
    }

    public bool IsLive => true;

    public async Task<string> StoreAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken);

        /*
         * The key is built here from a fresh GUID and nothing else the caller supplied.
         *
         * Not the file name: a name arrives from a browser and can contain path segments,
         * traversal, or another person's identifiers. The original name travels as
         * metadata instead, where it is data rather than a path.
         *
         * Date-prefixed so the container stays browsable and a retention sweep — when one
         * is written — can work by prefix rather than by listing everything.
         */
        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}";

        var blob = _container.GetBlobClient(key);

        await blob.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,

                    // Forces a download rather than letting a browser render the file
                    // inline, which is what stops an uploaded SVG or HTML file running
                    // script in the context of an admin's session.
                    ContentDisposition = "attachment",
                },
                Metadata = new Dictionary<string, string>
                {
                    // Sanitised: blob metadata must be ASCII, and this is displayed to
                    // staff rather than used as a path.
                    ["originalFileName"] = Sanitise(fileName),
                },
            },
            cancellationToken);

        // The key, never the name or the type: a log line about a background check should
        // not describe the background check.
        _logger.LogInformation("Stored certification document {DocumentKey}", key);

        return key;
    }

    public async Task<StoredDocument?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        var blob = _container.GetBlobClient(key);

        try
        {
            var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);

            var fileName = response.Value.Details.Metadata.TryGetValue("originalFileName", out var name)
                ? name
                : "certificate";

            return new StoredDocument(
                response.Value.Content,
                response.Value.Details.ContentType ?? "application/octet-stream",
                fileName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Gone, or never there. The caller decides what that means; here it is not an
            // error worth throwing over.
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        await _container
            .GetBlobClient(key)
            .DeleteIfExistsAsync(cancellationToken: cancellationToken);

        _logger.LogInformation("Deleted certification document {DocumentKey}", key);
    }

    /// <summary>
    /// Creates the container on first use, once.
    ///
    /// Gated because several caregivers uploading at the same moment would otherwise all
    /// race to create it. The call is idempotent, so the gate is about noise rather than
    /// correctness — but a container creation per upload is noise in an audit log.
    /// </summary>
    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_initialised)
        {
            return;
        }

        await _initGate.WaitAsync(cancellationToken);

        try
        {
            if (!_initialised)
            {
                await _container.CreateIfNotExistsAsync(
                    PublicAccessType.None, cancellationToken: cancellationToken);

                _initialised = true;
            }
        }
        finally
        {
            _initGate.Release();
        }
    }

    /// <summary>Keeps a browser-supplied file name to plain ASCII, short, and path-free.</summary>
    private static string Sanitise(string fileName)
    {
        var name = Path.GetFileName(fileName);

        var cleaned = new string(name
            .Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or ' ')
            .ToArray())
            .Trim();

        return cleaned.Length switch
        {
            0 => "certificate",
            > 120 => cleaned[..120],
            _ => cleaned,
        };
    }
}

/// <summary>
/// What runs when no storage is configured.
///
/// Deliberately refuses rather than pretending. Everywhere else in GoSit an unconfigured
/// provider degrades quietly — a missing geocoder costs distance ranking, a missing mailer
/// costs a notification. This one cannot: accepting an upload and dropping it would leave
/// a caregiver believing their vulnerable-sector check is on file and staff approving a
/// certification whose evidence never existed.
/// </summary>
public sealed class UnconfiguredDocumentStore : IDocumentStore
{
    public bool IsLive => false;

    public Task<string> StoreAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "No document storage is configured, so certification documents cannot be accepted.");

    public Task<StoredDocument?> ReadAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult<StoredDocument?>(null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
