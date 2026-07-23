using NorthernLink.Fleet.Domain.Shops;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Fleet.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="Shop"/> into <c>fleet.rm_shops</c>.</summary>
internal sealed class ShopProjection : FleetProjection<Shop, ShopReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(Shop));

    protected override void Map(Shop source, ShopReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Number = source.Number;
        row.Name = source.Name;
        row.ContactName = source.ContactName;
        row.Phone = source.Phone;
        row.Email = source.Email;
        row.Address = source.Address;
        row.GstBusinessNo = source.GstBusinessNo;
        row.MpiAccredited = source.MpiAccredited;
        row.InspectionStationNo = source.InspectionStationNo;
        row.SuppliesParts = source.SuppliesParts;
        row.Notes = source.Notes;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
