using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class ContractRateLineConfiguration : IEntityTypeConfiguration<ContractRateLine>
{
    public void Configure(EntityTypeBuilder<ContractRateLine> builder)
    {
        builder.ToTable("contract_rate_lines");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.ContractId).IsRequired();
        builder.Property(r => r.BillingCode).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(200).IsRequired();
        builder.Property(r => r.VehicleType).HasMaxLength(50).IsRequired();
        builder.Property(r => r.MaxDistanceKm);
        builder.Property(r => r.CargoIncluded).IsRequired();
        builder.Property(r => r.DayRate).HasPrecision(18, 2).IsRequired();

        builder.HasIndex(r => new { r.ContractId, r.BillingCode }).IsUnique();
    }
}
