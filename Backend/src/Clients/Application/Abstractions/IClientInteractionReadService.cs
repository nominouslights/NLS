using NorthernLink.Clients.Application.ClientInteractions;

namespace NorthernLink.Clients.Application.Abstractions;

/// <summary>Read side for interaction queries — returns response DTOs directly (tenant-scoped).</summary>
public interface IClientInteractionReadService
{
    /// <summary>Every interaction logged for one client, most recent first.</summary>
    Task<IReadOnlyList<ClientInteractionResponse>> GetForClientAsync(
        Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every open follow-up (FollowUpDate set) across all of the tenant's clients, earliest
    /// follow-up date first.
    /// </summary>
    Task<IReadOnlyList<ClientInteractionResponse>> GetOpenFollowUpsAsync(
        Guid tenantId, CancellationToken cancellationToken = default);
}
