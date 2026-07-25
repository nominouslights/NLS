using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Domain.Documents;

namespace NorthernLink.Fleet.Application.Documents.Add;

/// <summary>Adds a compliance document to a vehicle. Returns the new document's id.</summary>
public sealed record AddVehicleDocumentCommand(
    Guid TenantId,
    Guid VehicleId,
    DocumentType Type,
    string FileName,
    int FileSizeKb,
    string UploadedBy,
    DateTimeOffset? Expiry,
    string? Note) : ICommand<Guid>;
