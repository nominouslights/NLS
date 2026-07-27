using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Domain.ClientInteractions;

namespace NorthernLink.Clients.Application.ClientInteractions.Create;

/// <summary>Logs an interaction against a client. Returns the new interaction's id.</summary>
public sealed record CreateClientInteractionCommand(
    Guid TenantId,
    Guid ClientId,
    InteractionType Type,
    DateOnly OccurredOn,
    string Summary,
    IReadOnlyList<Guid> ParticipantContactIds,
    DateOnly? FollowUpDate,
    string? FollowUpNote) : ICommand<Guid>;
