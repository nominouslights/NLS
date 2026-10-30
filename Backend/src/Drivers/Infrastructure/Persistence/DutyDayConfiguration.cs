using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>
/// Maps the DutyDay aggregate to drivers.duty_days (snake_case columns). The events ride as
/// an owned jsonb collection — one row per day, no child table: a day is always read and
/// written whole, and the unique (tenant, driver, local_date) index is the "one record per
/// day" guarantee the whole ledger rests on. Enums inside the jsonb persist as their names, not
/// EF's default integers, because this payload is read by people and hand-written SQL.
/// </summary>
public sealed class DutyDayConfiguration : IEntityTypeConfiguration<DutyDay>
{
    public void Configure(EntityTypeBuilder<DutyDay> builder)
    {
        builder.ToTable("duty_days");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.DriverId).HasColumnName("driver_id");
        builder.Property(d => d.LocalDate).HasColumnName("local_date");
        builder.Property(d => d.LastSequence).HasColumnName("last_sequence");
        builder.Property(d => d.CertifiedAtUtc).HasColumnName("certified_at_utc");
        builder.Property(d => d.CertifiedByUserId).HasColumnName("certified_by_user_id");
        builder.Property(d => d.RecertificationRequired).HasColumnName("recertification_required");
        builder.Property(d => d.PersonalConveyanceKm).HasColumnName("personal_conveyance_km").HasPrecision(9, 1);
        builder.Property(d => d.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(d => d.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.Ignore(d => d.IsCertified);

        builder.OwnsMany(d => d.Events, e =>
        {
            e.ToJson("events");
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.Source).HasConversion<string>();
            e.Property(x => x.OdometerStartKm).HasPrecision(9, 1);
            e.Property(x => x.OdometerEndKm).HasPrecision(9, 1);
        });

        // One record per driver per day — the regulation's "two records" prohibition, in the
        // database rather than only in the handler's load-or-open.
        builder.HasIndex(d => new { d.TenantId, d.DriverId, d.LocalDate }).IsUnique();

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
