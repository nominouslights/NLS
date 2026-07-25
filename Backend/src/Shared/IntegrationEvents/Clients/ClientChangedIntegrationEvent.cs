using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Clients;

/// <summary>
/// Published whenever a client organization is created or renamed/recategorized —
/// routing key <c>clients.client-changed</c>. One event covers create and update so
/// consumers maintain a replica by upserting on <see cref="ClientId"/> (idempotent
/// under at-least-once delivery). Trips consumes it to keep its <c>client_lookup</c>
/// table current for trip/template client snapshots without a library reference.
/// Type travels as a string ("Client" or "VendorPartner"); ServiceType travels as a
/// string ("ContractCrew", "Community", "Nihb", "Charter", "Cargo", "Grocery") — null
/// for VendorPartner organizations. Integration events never reference Clients'
/// internal enums. <see cref="TenantId"/> is part of the payload because handlers run
/// outside any HTTP request.
/// </summary>
public sealed record ClientChangedIntegrationEvent(
    Guid ClientId,
    Guid TenantId,
    string Name,
    string Type,
    string? ServiceType,
    string Tag) : IntegrationEvent;
