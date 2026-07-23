using NorthernLink.Drivers.Domain.Clearances;
using NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Drivers.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="DriverClearance"/> into <c>drivers.rm_driver_clearances</c>.</summary>
internal sealed class DriverClearanceProjection : DriversProjection<DriverClearance, DriverClearanceReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(DriverClearance));

    protected override void Map(DriverClearance source, DriverClearanceReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.DriverId = source.DriverId;
        row.Title = source.Title;
        row.ClientName = source.ClientName;
        row.Expiry = source.Expiry;
        row.GrantedAtUtc = source.GrantedAtUtc;
        row.Version = source.Version;
    }
}
