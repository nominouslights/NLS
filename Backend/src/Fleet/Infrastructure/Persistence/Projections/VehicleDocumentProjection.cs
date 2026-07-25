using NorthernLink.Fleet.Domain.Documents;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Fleet.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="VehicleDocument"/> into <c>fleet.rm_vehicle_documents</c>.</summary>
internal sealed class VehicleDocumentProjection : FleetProjection<VehicleDocument, VehicleDocumentReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(VehicleDocument));

    protected override void Map(VehicleDocument source, VehicleDocumentReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.VehicleId = source.VehicleId;
        row.Number = source.Number;
        row.Type = source.Type.ToString();
        row.FileName = source.FileName;
        row.FileSizeKb = source.FileSizeKb;
        row.UploadedBy = source.UploadedBy;
        row.UploadedAt = source.UploadedAt;
        row.Expiry = source.Expiry;
        row.Note = source.Note;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.Version = source.Version;
    }
}
