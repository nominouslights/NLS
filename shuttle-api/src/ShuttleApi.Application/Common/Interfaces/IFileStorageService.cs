namespace ShuttleApi.Application.Common.Interfaces;

/// <summary>
/// Abstraction for file storage. The current implementation stores blobs in the database
/// (DatabaseFileStorageService). When blob storage is provisioned, swap the registration
/// in Infrastructure/DependencyInjection.cs — no other code changes are required.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Stores file bytes and returns an opaque storage key.</summary>
    Task<string> StoreAsync(string fileName, string contentType, byte[] data, CancellationToken cancellationToken = default);

    /// <summary>Retrieves file bytes by the storage key returned from StoreAsync.</summary>
    Task<FileStorageResult> RetrieveAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes the file identified by storageKey.</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}

public sealed record FileStorageResult(string FileName, string ContentType, byte[] Data);
