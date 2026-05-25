using Microsoft.EntityFrameworkCore;
using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Common;
using ShuttleApi.Infrastructure.Persistence;

namespace ShuttleApi.Infrastructure.Services;

/// <summary>
/// File storage implementation that persists blobs in the document_file_blobs database table.
/// To migrate to Azure Blob Storage or AWS S3, create a new implementation of IFileStorageService,
/// register it instead of this class, and run a one-time migration job to move existing blobs.
/// </summary>
internal sealed class DatabaseFileStorageService(AppDbContext dbContext) : IFileStorageService
{
    private const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public async Task<string> StoreAsync(
        string fileName,
        string contentType,
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        if (data.Length > MaxFileSizeBytes)
            throw new InvalidOperationException($"File size {data.Length} exceeds the maximum allowed size of {MaxFileSizeBytes} bytes.");

        var storageKey = Guid.NewGuid().ToString();
        var blob = DocumentFileBlob.Create(storageKey, fileName, contentType, data);

        await dbContext.DocumentFileBlobs.AddAsync(blob, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return storageKey;
    }

    public async Task<FileStorageResult> RetrieveAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var blob = await dbContext.DocumentFileBlobs
            .FirstOrDefaultAsync(b => b.StorageKey == storageKey, cancellationToken);

        if (blob is null)
            throw new KeyNotFoundException($"No file found with storage key '{storageKey}'.");

        return new FileStorageResult(blob.FileName, blob.ContentType, blob.FileData);
    }

    public async Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var blob = await dbContext.DocumentFileBlobs
            .FirstOrDefaultAsync(b => b.StorageKey == storageKey, cancellationToken);

        if (blob is not null)
        {
            dbContext.DocumentFileBlobs.Remove(blob);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
