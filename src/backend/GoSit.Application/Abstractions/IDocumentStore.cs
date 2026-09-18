namespace GoSit.Application.Abstractions;

/// <summary>A stored document, streamed back for someone entitled to read it.</summary>
public sealed record StoredDocument(Stream Content, string ContentType, string FileName);

/// <summary>
/// Private storage for certification evidence.
///
/// Everything here is a background check, a criminal record certificate, or a first-aid
/// card belonging to a real person. CLAUDE.md forbids exposing background-check documents,
/// and the shape of this interface is what enforces that: there is no method that returns
/// a URL. Nothing can be linked to, emailed, or made public, because nothing here can
/// produce a public address for a blob.
///
/// Reading is a server-side stream, so every read passes through the API's own
/// authorisation. A shared access signature would be simpler and would also be a bearer
/// token for a vulnerable-sector check that works for anyone who ends up holding it.
/// </summary>
public interface IDocumentStore
{
    /// <summary>True when real storage is configured, rather than the stand-in.</summary>
    bool IsLive { get; }

    /// <summary>
    /// Stores a document and returns the key it was given.
    ///
    /// The key is assigned here, never accepted from a caller. A client-supplied key is a
    /// client-supplied path, and a caregiver who can name their own key can name someone
    /// else's — which is how one caregiver's profile ends up citing another's criminal
    /// record check as evidence.
    /// </summary>
    Task<string> StoreAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);

    /// <summary>Null when the key is unknown — a deleted document is not an error.</summary>
    Task<StoredDocument?> ReadAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
