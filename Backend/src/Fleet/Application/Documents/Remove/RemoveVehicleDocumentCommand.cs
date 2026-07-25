using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Documents.Remove;

/// <summary>Removes a compliance document.</summary>
public sealed record RemoveVehicleDocumentCommand(Guid TenantId, Guid DocumentId) : ICommand;
