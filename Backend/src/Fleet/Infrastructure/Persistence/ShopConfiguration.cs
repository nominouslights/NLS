using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Fleet.Domain.Shops;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Maps the Shop aggregate to fleet.shops (snake_case columns).</summary>
public sealed class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("shops");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.Number).HasColumnName("number").HasMaxLength(16);
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(s => s.ContactName).HasColumnName("contact_name").HasMaxLength(128);
        builder.Property(s => s.Phone).HasColumnName("phone").HasMaxLength(32);
        builder.Property(s => s.Email).HasColumnName("email").HasMaxLength(256);
        builder.Property(s => s.Address).HasColumnName("address").HasMaxLength(500);
        builder.Property(s => s.GstBusinessNo).HasColumnName("gst_business_no").HasMaxLength(64);
        builder.Property(s => s.MpiAccredited).HasColumnName("mpi_accredited");
        builder.Property(s => s.InspectionStationNo).HasColumnName("inspection_station_no").HasMaxLength(64);
        builder.Property(s => s.SuppliesParts).HasColumnName("supplies_parts");
        builder.Property(s => s.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(s => new { s.TenantId, s.Number }).IsUnique();
    }
}
