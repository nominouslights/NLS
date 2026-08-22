using NorthernLink.Clients.Domain.ClientContacts;
using NorthernLink.Clients.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Clients.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="ClientContact"/> into <c>clients.rm_client_contacts</c>. No
/// denormalization, no complex joins — a direct 1:1 projection.
/// </summary>
internal sealed class ClientContactProjection : ClientsProjection<ClientContact, ClientContactReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(ClientContact));

    protected override void Map(ClientContact source, ClientContactReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.ClientId = source.ClientId;
        row.Name = source.Name;
        row.Title = source.Title;
        row.Email = source.Email;
        row.Phone = source.Phone;
        row.Notes = source.Notes;
        row.IsPrimary = source.IsPrimary;
        row.ReceivesEmailReports = source.ReceivesEmailReports;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
