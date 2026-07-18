using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Documents;

namespace NorthernLink.Fleet.Application.Documents.GetAll;

/// <summary>Lists every compliance document for the tenant (dashboard compliance watch).</summary>
public sealed record GetAllDocumentsQuery(Guid TenantId) : IQuery<IReadOnlyList<VehicleDocumentResponse>>;
