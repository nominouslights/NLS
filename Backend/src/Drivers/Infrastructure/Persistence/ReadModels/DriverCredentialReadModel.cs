using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;

/// <summary>Read-side projection of a driver credential into <c>drivers.rm_driver_credentials</c>.</summary>
public sealed class DriverCredentialReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DriverId { get; set; }
    public string Type { get; set; } = null!;
    public string Label { get; set; } = null!;
    public DateOnly Issued { get; set; }
    public DateOnly? Expiry { get; set; }
    public bool Optional { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public int Version { get; set; }
    public string? ImageKey { get; set; }
    public string? ImageContentType { get; set; }
}

public sealed class DriverCredentialReadModelConfiguration : IEntityTypeConfiguration<DriverCredentialReadModel>
{
    public void Configure(EntityTypeBuilder<DriverCredentialReadModel> builder)
    {
        builder.HasKey(c => c.Id);
        builder.ToTable("rm_driver_credentials", DriversServiceCollectionExtensions.SchemaName);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.DriverId).HasColumnName("driver_id");
        builder.Property(c => c.Type).HasColumnName("type");
        builder.Property(c => c.Label).HasColumnName("label");
        builder.Property(c => c.Issued).HasColumnName("issued");
        builder.Property(c => c.Expiry).HasColumnName("expiry");
        builder.Property(c => c.Optional).HasColumnName("optional");
        builder.Property(c => c.Note).HasColumnName("note");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.Version).HasColumnName("version");
        builder.Property(c => c.ImageKey).HasColumnName("image_key");
        builder.Property(c => c.ImageContentType).HasColumnName("image_content_type");
    }
}
