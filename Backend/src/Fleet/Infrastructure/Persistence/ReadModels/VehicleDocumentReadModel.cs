using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

/// <summary>Read-side projection of a vehicle document, via <c>fleet.mv_vehicle_documents</c> / <c>fleet.v_vehicle_documents</c>.</summary>
public sealed class VehicleDocumentReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VehicleId { get; set; }
    public string Number { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public int FileSizeKb { get; set; }
    public string UploadedBy { get; set; } = null!;
    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset? Expiry { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class VehicleDocumentReadModelConfiguration : IEntityTypeConfiguration<VehicleDocumentReadModel>
{
    public void Configure(EntityTypeBuilder<VehicleDocumentReadModel> builder)
    {
        builder.HasNoKey().ToView("v_vehicle_documents", FleetServiceCollectionExtensions.SchemaName);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.VehicleId).HasColumnName("vehicle_id");
        builder.Property(d => d.Number).HasColumnName("number");
        builder.Property(d => d.Type).HasColumnName("type");
        builder.Property(d => d.FileName).HasColumnName("file_name");
        builder.Property(d => d.FileSizeKb).HasColumnName("file_size_kb");
        builder.Property(d => d.UploadedBy).HasColumnName("uploaded_by");
        builder.Property(d => d.UploadedAt).HasColumnName("uploaded_at");
        builder.Property(d => d.Expiry).HasColumnName("expiry");
        builder.Property(d => d.Note).HasColumnName("note");
        builder.Property(d => d.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(d => d.Version).HasColumnName("version");
    }
}
