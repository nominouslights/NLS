using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// One row per tenant backing the "SH-####" sequence. Only ever touched by
/// <see cref="ShipmentNumberGenerator"/>'s atomic upsert — mapped as an entity solely so
/// migrations create the table and the model snapshot stays authoritative. The exact shape of
/// <see cref="TripNumberCounter"/>, deliberately: two sequences, one proven pattern.
/// </summary>
public sealed class ShipmentNumberCounter
{
    public Guid TenantId { get; set; }
    public long NextValue { get; set; }
}

public sealed class ShipmentNumberCounterConfiguration : IEntityTypeConfiguration<ShipmentNumberCounter>
{
    public void Configure(EntityTypeBuilder<ShipmentNumberCounter> builder)
    {
        builder.ToTable("shipment_number_counters");

        builder.HasKey(c => c.TenantId);
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(c => c.NextValue).HasColumnName("next_value");
    }
}
