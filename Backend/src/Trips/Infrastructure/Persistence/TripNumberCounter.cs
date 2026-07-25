using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// One row per tenant backing the "TR-####" sequence. Only ever touched by
/// <see cref="TripNumberGenerator"/>'s atomic upsert — mapped as an entity solely so
/// migrations create the table and the model snapshot stays authoritative.
/// </summary>
public sealed class TripNumberCounter
{
    public Guid TenantId { get; set; }
    public long NextValue { get; set; }
}

public sealed class TripNumberCounterConfiguration : IEntityTypeConfiguration<TripNumberCounter>
{
    public void Configure(EntityTypeBuilder<TripNumberCounter> builder)
    {
        builder.ToTable("trip_number_counters");

        builder.HasKey(c => c.TenantId);
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(c => c.NextValue).HasColumnName("next_value");
    }
}
