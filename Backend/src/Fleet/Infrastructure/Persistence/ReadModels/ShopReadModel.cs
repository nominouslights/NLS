using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

/// <summary>Read-side projection of a shop, via <c>fleet.mv_shops</c> / <c>fleet.v_shops</c>.</summary>
public sealed class ShopReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Number { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? GstBusinessNo { get; set; }
    public bool MpiAccredited { get; set; }
    public string? InspectionStationNo { get; set; }
    public bool SuppliesParts { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class ShopReadModelConfiguration : IEntityTypeConfiguration<ShopReadModel>
{
    public void Configure(EntityTypeBuilder<ShopReadModel> builder)
    {
        builder.HasNoKey().ToView("v_shops", FleetServiceCollectionExtensions.SchemaName);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.Number).HasColumnName("number");
        builder.Property(s => s.Name).HasColumnName("name");
        builder.Property(s => s.ContactName).HasColumnName("contact_name");
        builder.Property(s => s.Phone).HasColumnName("phone");
        builder.Property(s => s.Email).HasColumnName("email");
        builder.Property(s => s.Address).HasColumnName("address");
        builder.Property(s => s.GstBusinessNo).HasColumnName("gst_business_no");
        builder.Property(s => s.MpiAccredited).HasColumnName("mpi_accredited");
        builder.Property(s => s.InspectionStationNo).HasColumnName("inspection_station_no");
        builder.Property(s => s.SuppliesParts).HasColumnName("supplies_parts");
        builder.Property(s => s.Notes).HasColumnName("notes");
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(s => s.Version).HasColumnName("version");
    }
}
