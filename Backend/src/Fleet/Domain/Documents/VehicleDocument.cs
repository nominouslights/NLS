using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Documents;

/// <summary>
/// A compliance document attached to a vehicle (registration, insurance, NSC safety
/// certificate, …). Metadata only — the file bytes / object storage are deferred; today
/// only the file name and size are recorded. <see cref="Number"/> (DOC-…) is the business
/// key. Compliance status (valid/expiring/expired) is derived by the frontend from
/// <see cref="Expiry"/>, so it is not stored.
/// </summary>
public sealed class VehicleDocument : AggregateRoot, ITenantScoped
{
    private VehicleDocument()
    {
        // EF Core materialization only.
        Number = null!;
        FileName = null!;
        UploadedBy = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid VehicleId { get; private set; }
    public string Number { get; private set; }
    public DocumentType Type { get; private set; }
    public string FileName { get; private set; }
    public int FileSizeKb { get; private set; }
    public string UploadedBy { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public DateTimeOffset? Expiry { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<VehicleDocument> Add(
        Guid tenantId,
        Guid vehicleId,
        string number,
        DocumentType type,
        string fileName,
        int fileSizeKb,
        string uploadedBy,
        DateTimeOffset? expiry,
        string? note)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Result.Failure<VehicleDocument>(DocumentErrors.FileNameRequired);
        }

        if (fileSizeKb < 0)
        {
            return Result.Failure<VehicleDocument>(DocumentErrors.InvalidFileSize);
        }

        var document = new VehicleDocument
        {
            TenantId = tenantId,
            VehicleId = vehicleId,
            Number = number,
            Type = type,
            FileName = fileName.Trim(),
            FileSizeKb = fileSizeKb,
            UploadedBy = string.IsNullOrWhiteSpace(uploadedBy) ? "Dispatch" : uploadedBy.Trim(),
            UploadedAt = DateTimeOffset.UtcNow,
            Expiry = expiry,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        return Result.Success(document);
    }
}
