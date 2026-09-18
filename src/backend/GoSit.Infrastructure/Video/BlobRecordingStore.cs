using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using GoSit.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoSit.Infrastructure.Video;

/// <summary>
/// Interview recordings in Azure Blob Storage.
///
/// LiveKit writes the files; this reads them for staff and deletes them when their time is
/// up. The container is created private and never made public — what is in here is video
/// of people, often in their homes.
/// </summary>
public sealed class BlobRecordingStore : IRecordingStore
{
    /// <summary>Pinned, for the reason given on the document store.</summary>
    private const BlobClientOptions.ServiceVersion ApiVersion = BlobClientOptions.ServiceVersion.V2025_11_05;

    private readonly BlobContainerClient _container;
    private readonly ILogger<BlobRecordingStore> _logger;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private bool _initialised;

    public BlobRecordingStore(IOptions<InterviewRecordingOptions> options, ILogger<BlobRecordingStore> logger)
    {
        _container = new BlobServiceClient(options.Value.ConnectionString, new BlobClientOptions(ApiVersion))
            .GetBlobContainerClient(options.Value.ContainerName);

        _logger = logger;
    }

    public bool IsLive => true;

    public async Task PrepareAsync(CancellationToken cancellationToken)
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
                await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
                _initialised = true;
            }
        }
        finally
        {
            _initGate.Release();
        }
    }

    public async Task<StoredDocument?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.GetBlobClient(key).DownloadStreamingAsync(cancellationToken: cancellationToken);

            return new StoredDocument(response.Value.Content, "video/mp4", "interview-recording.mp4");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        await _container.GetBlobClient(key).DeleteIfExistsAsync(
            DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);

        // Not the key: it names the interview.
        _logger.LogInformation("Deleted an interview recording from storage");
    }
}

/// <summary>No storage configured. Interviews cannot run, so there is nothing to read or delete.</summary>
public sealed class UnconfiguredRecordingStore : IRecordingStore
{
    public bool IsLive => false;

    public Task PrepareAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No interview recording storage is configured.");

    public Task<StoredDocument?> ReadAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult<StoredDocument?>(null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No interview recording storage is configured.");
}
