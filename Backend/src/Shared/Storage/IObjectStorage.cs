namespace NorthernLink.Shared.Storage;

/// <summary>
/// Abstraction over object storage (S3-compatible or otherwise) for persisting binary media
/// (credential photos, DVIR photos, signatures, etc.) outside the database. Implementations
/// handle the authentication, endpoint configuration, and retry logic; callers pass a stream
/// and content-type, never worry about buckets/regions/keys.
/// </summary>
public interface IObjectStorage
{
    /// <summary>
    /// Store a binary object under <paramref name="key"/> with the given content-type.
    /// Overwrites if the key already exists.
    /// </summary>
    Task PutAsync(string key, Stream stream, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a binary object from <paramref name="key"/>. Returns a stream the caller
    /// owns responsibility for disposing.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown if the key does not exist.</exception>
    Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a binary object at <paramref name="key"/>. No-op if the key doesn't exist.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
