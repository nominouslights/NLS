using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class TripPostReportConfiguration : IEntityTypeConfiguration<TripPostReport>
{
    public void Configure(EntityTypeBuilder<TripPostReport> builder)
    {
        builder.ToTable("trip_post_reports");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.TripId).IsRequired();
        builder.Property(r => r.OdometerStart).IsRequired();
        builder.Property(r => r.OdometerEnd).IsRequired();
        builder.Property(r => r.FuelAddedLitres).HasPrecision(8, 2);
        builder.Property(r => r.FuelCostDollars).HasPrecision(10, 2);
        builder.Property(r => r.HasIncident).IsRequired();
        builder.Property(r => r.IncidentType)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.Property(r => r.IncidentDescription).HasMaxLength(2000);
        builder.Property(r => r.AdditionalNotes).HasMaxLength(2000);
        builder.Property(r => r.SubmittedAt).IsRequired();
        builder.Property(r => r.IsReadyToInvoice).IsRequired();

        builder.Property(r => r.ExteriorNoNewDamage).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.InteriorCleanedAndChecked).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.PassengersDisembarkedSafely).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.AllCargoDeliveredAndAccounted).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.VehicleSecuredAndPluggedIn).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.KeysReturnedAndSecured).IsRequired().HasDefaultValue(false);

        // Computed property — not persisted
        builder.Ignore(r => r.DistanceKm);
    }
}
