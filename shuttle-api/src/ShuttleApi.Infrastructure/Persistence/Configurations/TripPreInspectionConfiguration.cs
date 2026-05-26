using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class TripPreInspectionConfiguration : IEntityTypeConfiguration<TripPreInspection>
{
    public void Configure(EntityTypeBuilder<TripPreInspection> builder)
    {
        builder.ToTable("trip_pre_inspections");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.TripId).IsRequired();
        builder.Property(p => p.OdometerStart).IsRequired();
        builder.Property(p => p.SubmittedAt).IsRequired();

        builder.HasMany(p => p.Items)
            .WithOne()
            .HasForeignKey(i => i.PreInspectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
