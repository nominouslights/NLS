using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Domain.Clients;

namespace NorthernLink.Clients.Application.Clients.Create;

/// <summary>Creates a new client organization. Returns the new client's id.</summary>
public sealed record CreateClientCommand(
    Guid TenantId,
    string Name,
    ClientType Type,
    ClientServiceType? ServiceType,
    string Tag,
    string? AgreementReference,
    string? Notes) : ICommand<Guid>;
