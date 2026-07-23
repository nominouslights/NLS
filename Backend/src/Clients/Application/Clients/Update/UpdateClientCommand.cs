using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Domain.Clients;

namespace NorthernLink.Clients.Application.Clients.Update;

/// <summary>Updates an existing client organization's details.</summary>
public sealed record UpdateClientCommand(
    Guid TenantId,
    Guid ClientId,
    string Name,
    ClientServiceType? ServiceType,
    string Tag,
    string? AgreementReference,
    string? Notes) : ICommand;
