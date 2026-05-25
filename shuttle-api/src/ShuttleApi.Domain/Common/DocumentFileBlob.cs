namespace ShuttleApi.Domain.Common;

/// <summary>
/// Stores raw file bytes for the database-backed file storage implementation.
/// When blob storage (Azure/AWS) is adopted, this entity and its table will be
/// removed after a one-time migration job moves all data to the blob container.
/// </summary>
public sealed class DocumentFileBlob : Entity<Guid>
{
    /// <summary>Opaque key used to look up this blob. Matches DriverDocument.StorageKey.</summary>
    public string StorageKey { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public byte[] FileData { get; private set; } = [];
    public long FileSizeBytes { get; private set; }
    public DateTime StoredAt { get; private set; }

    private DocumentFileBlob() { }

    public static DocumentFileBlob Create(
        string storageKey,
        string fileName,
        string contentType,
        byte[] fileData)
    {
        return new DocumentFileBlob
        {
            Id = Guid.NewGuid(),
            StorageKey = storageKey,
            FileName = fileName,
            ContentType = contentType,
            FileData = fileData,
            FileSizeBytes = fileData.LongLength,
            StoredAt = DateTime.UtcNow
        };
    }
}
