using NorthernLink.Clients.Domain.ClientInteractions;
using NorthernLink.Clients.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Clients.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="ClientInteraction"/> into <c>clients.rm_client_interactions</c>. Hard
/// deletes propagate through the base's source-gone path, driven by the synthetic
/// aggregate-deleted journal row the audit pipeline writes.
/// </summary>
internal sealed class ClientInteractionProjection : ClientsProjection<ClientInteraction, ClientInteractionReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(ClientInteraction));

    protected override void Map(ClientInteraction source, ClientInteractionReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.ClientId = source.ClientId;
        row.Type = source.Type;
        row.OccurredOn = source.OccurredOn;
        row.Summary = source.Summary;
        row.ParticipantContactIds = source.ParticipantContactIds.ToList();
        row.FollowUpDate = source.FollowUpDate;
        row.FollowUpNote = source.FollowUpNote;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
