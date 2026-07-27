using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.ClientInteractions.GetOpenFollowUps;

/// <summary>Open follow-ups across every client of the tenant, earliest follow-up date first.</summary>
public sealed record GetOpenFollowUpsQuery(Guid TenantId)
    : IQuery<IReadOnlyList<ClientInteractionResponse>>;
