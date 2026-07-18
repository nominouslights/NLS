using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Documents;

namespace NorthernLink.Fleet.Application.Documents.GetForVehicle;

/// <summary>Lists a vehicle's compliance documents.</summary>
public sealed record GetVehicleDocumentsQuery(Guid TenantId, Guid VehicleId)
    : IQuery<IReadOnlyList<VehicleDocumentResponse>>;
