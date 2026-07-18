namespace NorthernLink.Fleet.Application.Documents;

/// <summary>
/// The Fleet module's public representation of a vehicle compliance document.
/// <paramref name="Type"/> is the DocumentType enum name (e.g. "InsuranceMpi"); the
/// frontend maps it to a display label and derives the compliance chip from Expiry.
/// </summary>
public sealed record VehicleDocumentResponse(
    Guid Id,
    Guid VehicleId,
    string Number,
    string Type,
    string FileName,
    int FileSizeKb,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    DateTimeOffset? Expiry,
    string? Note);
