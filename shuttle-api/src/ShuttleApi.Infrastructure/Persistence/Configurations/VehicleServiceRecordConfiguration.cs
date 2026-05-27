using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class VehicleServiceRecordConfiguration : IEntityTypeConfiguration<VehicleServiceRecord>
{
    public void Configure(EntityTypeBuilder<VehicleServiceRecord> builder)
    {
        builder.ToTable("vehicle_service_records");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.VehicleId).IsRequired();

        builder.Property(r => r.ServiceCategory)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // FluidType is nullable — only set when ServiceCategory == FluidChange
        builder.Property(r => r.FluidType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(r => r.Title).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.IsPlanned).IsRequired();

        builder.Property(r => r.ServiceStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.ScheduledDate);
        builder.Property(r => r.StartedDate);
        builder.Property(r => r.CompletedDate);
        builder.Property(r => r.OdometerAtService);

        builder.Property(r => r.EstimatedCostDollars).HasPrecision(10, 2);
        builder.Property(r => r.ActualCostDollars).HasPrecision(10, 2);

        builder.Property(r => r.ServiceProvider).HasMaxLength(200);
        builder.Property(r => r.TechnicianName).HasMaxLength(200);
        builder.Property(r => r.PartsNotes).HasMaxLength(2000);
        builder.Property(r => r.IsWarrantyWork).IsRequired();

        builder.Property(r => r.NextServiceDueDateUtc);
        builder.Property(r => r.NextServiceDueOdometerKm);

        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasIndex(r => r.VehicleId);
        builder.HasIndex(r => new { r.VehicleId, r.ServiceCategory });
    }
}
