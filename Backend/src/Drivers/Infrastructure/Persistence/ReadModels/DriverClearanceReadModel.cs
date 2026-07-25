using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;

/// <summary>Read-side projection of a driver clearance into <c>drivers.rm_driver_clearances</c>.</summary>
public sealed class DriverClearanceReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DriverId { get; set; }
    public string Title { get; set; } = null!;
    public string ClientName { get; set; } = null!;
    public DateOnly? Expiry { get; set; }
    public DateTimeOffset GrantedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class DriverClearanceReadModelConfiguration : IEntityTypeConfiguration<DriverClearanceReadModel>
{
    public void Configure(EntityTypeBuilder<DriverClearanceReadModel> builder)
    {
        builder.HasKey(c => c.Id);
        builder.ToTable("rm_driver_clearances", DriversServiceCollectionExtensions.SchemaName);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.DriverId).HasColumnName("driver_id");
        builder.Property(c => c.Title).HasColumnName("title");
        builder.Property(c => c.ClientName).HasColumnName("client_name");
        builder.Property(c => c.Expiry).HasColumnName("expiry");
        builder.Property(c => c.GrantedAtUtc).HasColumnName("granted_at_utc");
        builder.Property(c => c.Version).HasColumnName("version");
    }
}
