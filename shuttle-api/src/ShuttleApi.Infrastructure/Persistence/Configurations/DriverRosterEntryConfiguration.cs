using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Infrastructure.Persistence.Configurations;

public sealed class DriverRosterEntryConfiguration : IEntityTypeConfiguration<DriverRosterEntry>
{
    public void Configure(EntityTypeBuilder<DriverRosterEntry> builder)
    {
        builder.ToTable("driver_roster_entries");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.DriverId).IsRequired();
        builder.Property(r => r.EntryDate).HasColumnType("date").IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.ShiftStart).HasColumnType("time without time zone");
        builder.Property(r => r.ShiftEnd).HasColumnType("time without time zone");
        builder.Property(r => r.UpdatedAt).IsRequired();

        // One entry per driver per day
        builder.HasIndex(r => new { r.DriverId, r.EntryDate }).IsUnique();
    }
}
