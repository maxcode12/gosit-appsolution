namespace GoSit.Infrastructure.Video;

/// <summary>
/// The LiveKit project interviews run on.
///
/// The API key and secret are secrets in every environment — Key Vault when deployed, user
/// secrets locally. The server URL is the project's "wss://" address from the LiveKit
/// dashboard; the API calls go to the same host over HTTPS.
/// </summary>
public sealed class LiveKitOptions
{
    public const string SectionName = "LiveKit";

    public string ServerUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ServerUrl)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ApiSecret);
}

/// <summary>
/// Where interview recordings are kept: Azure Blob Storage, in a container of their own.
///
/// A connection string with an account name and key, because LiveKit uploads the file
/// directly and needs credentials it can use from outside GoSit. It must be a real Azure
/// account — LiveKit Cloud cannot reach a local Azurite.
/// </summary>
public sealed class InterviewRecordingOptions
{
    public const string SectionName = "InterviewRecordings";

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Kept apart from certification documents, so who can read what stays a per-container rule.</summary>
    public string ContainerName { get; set; } = "interview-recordings";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}

/// <summary>The account name and key inside an Azure Storage connection string.</summary>
internal sealed record AzureStorageCredentials(string AccountName, string AccountKey)
{
    public static AzureStorageCredentials Parse(string connectionString)
    {
        var parts = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.OrdinalIgnoreCase);

        return parts.TryGetValue("AccountName", out var name) && parts.TryGetValue("AccountKey", out var key)
            ? new AzureStorageCredentials(name, key)
            : throw new InvalidOperationException(
                "The interview recording connection string must include an account name and key.");
    }
}
