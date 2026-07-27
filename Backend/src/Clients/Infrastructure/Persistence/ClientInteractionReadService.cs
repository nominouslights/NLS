using Microsoft.EntityFrameworkCore;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Application.ClientInteractions;
using NorthernLink.Clients.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Read side — queries clients.rm_client_interactions and maps to the public contract.</summary>
internal sealed class ClientInteractionReadService(ClientsDbContext context) : IClientInteractionReadService
{
    public async Task<IReadOnlyList<ClientInteractionResponse>> GetForClientAsync(
        Guid tenantId,
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        var interactions = await context.ClientInteractionReadModels
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.ClientId == clientId)
            .OrderByDescending(i => i.OccurredOn)
            .ToListAsync(cancellationToken);

        return interactions.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<ClientInteractionResponse>> GetOpenFollowUpsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var followUps = await context.ClientInteractionReadModels
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.FollowUpDate != null)
            .OrderBy(i => i.FollowUpDate)
            .ToListAsync(cancellationToken);

        return followUps.Select(ToResponse).ToList();
    }

    private static ClientInteractionResponse ToResponse(ClientInteractionReadModel i) => new(
        i.Id,
        i.ClientId,
        InteractionTypeWire.ToWire(i.Type),
        i.OccurredOn,
        i.Summary,
        i.ParticipantContactIds,
        i.FollowUpDate,
        i.FollowUpNote,
        i.CreatedAtUtc,
        i.UpdatedAtUtc);
}
