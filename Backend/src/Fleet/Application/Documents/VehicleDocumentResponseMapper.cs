using NorthernLink.Fleet.Domain.Documents;

namespace NorthernLink.Fleet.Application.Documents;

/// <summary>Maps the VehicleDocument aggregate to its public response contract.</summary>
public static class VehicleDocumentResponseMapper
{
    public static VehicleDocumentResponse ToResponse(VehicleDocument document) => new(
        document.Id,
        document.VehicleId,
        document.Number,
        document.Type.ToString(),
        document.FileName,
        document.FileSizeKb,
        document.UploadedBy,
        document.UploadedAt,
        document.Expiry,
        document.Note);
}
