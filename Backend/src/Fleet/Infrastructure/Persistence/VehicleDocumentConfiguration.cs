using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Documents;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Maps the VehicleDocument aggregate to fleet.vehicle_documents (snake_case columns).</summary>
public sealed class VehicleDocumentConfiguration : IEntityTypeConfiguration<VehicleDocument>
{
    public void Configure(EntityTypeBuilder<VehicleDocument> builder)
    {
        builder.ToTable("vehicle_documents");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.VehicleId).HasColumnName("vehicle_id");
        builder.Property(d => d.Number).HasColumnName("number").HasMaxLength(16);

        builder.Property(d => d.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(d => d.FileName).HasColumnName("file_name").HasMaxLength(300);
        builder.Property(d => d.FileSizeKb).HasColumnName("file_size_kb");
        builder.Property(d => d.UploadedBy).HasColumnName("uploaded_by").HasMaxLength(128);
        builder.Property(d => d.UploadedAt).HasColumnName("uploaded_at");
        builder.Property(d => d.Expiry).HasColumnName("expiry");
        builder.Property(d => d.Note).HasColumnName("note").HasMaxLength(1000);
        builder.Property(d => d.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(d => new { d.TenantId, d.Number }).IsUnique();
        builder.HasIndex(d => new { d.TenantId, d.VehicleId });
    }
}
