using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a driver into <c>drivers.rm_drivers</c>, maintained by
/// <c>DriverProjection</c> (and refreshed by <c>DriverCredentialProjection</c> when
/// credentials change). <see cref="CredentialCount"/> and
/// <see cref="SoonestCredentialExpiry"/> are denormalized from the driver's credentials
/// so the roster list needs one query; expiry status chips are derived by the frontend
/// from the dates, never stored. <see cref="Version"/> is the aggregate's
/// optimistic-concurrency version captured at last projection.
/// </summary>
public sealed class DriverReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string? Phone { get; set; }
    public string LicenceClass { get; set; } = null!;
    public DateOnly? LicenceExpiry { get; set; }
    public string Source { get; set; } = null!;
    public bool HasWorkPermit { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset RegisteredAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }

    // Denormalized from the driver's credentials by the projections.
    public int CredentialCount { get; set; }
    public DateOnly? SoonestCredentialExpiry { get; set; }

    // Denormalized from the driver's newest HOS entry by the projections (null until the
    // first entry). LatestDutyStatus is the friendly duty string the roster's duty chip
    // keys on; LatestDrivingHours feeds the roster's "HOS left" gauge.
    public string? LatestDutyStatus { get; set; }
    public decimal? LatestDrivingHours { get; set; }
    public DateOnly? LatestHosDate { get; set; }
}

/// <summary>
/// Maps <see cref="DriverReadModel"/> to drivers.rm_drivers. The tenant query filter is
/// applied centrally in <c>DriversDbContext.ConfigureModule</c> so it references the
/// context's per-instance TenantId, exactly like the aggregate filters.
/// </summary>
public sealed class DriverReadModelConfiguration : IEntityTypeConfiguration<DriverReadModel>
{
    public void Configure(EntityTypeBuilder<DriverReadModel> builder)
    {
        builder.HasKey(d => d.Id);
        builder.ToTable("rm_drivers", DriversServiceCollectionExtensions.SchemaName);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.Name).HasColumnName("name");
        builder.Property(d => d.Phone).HasColumnName("phone");
        builder.Property(d => d.LicenceClass).HasColumnName("licence_class");
        builder.Property(d => d.LicenceExpiry).HasColumnName("licence_expiry");
        builder.Property(d => d.Source).HasColumnName("source");
        builder.Property(d => d.HasWorkPermit).HasColumnName("has_work_permit");
        builder.Property(d => d.Status).HasColumnName("status");
        builder.Property(d => d.RegisteredAtUtc).HasColumnName("registered_at_utc");
        builder.Property(d => d.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(d => d.Version).HasColumnName("version");
        builder.Property(d => d.CredentialCount).HasColumnName("credential_count");
        builder.Property(d => d.SoonestCredentialExpiry).HasColumnName("soonest_credential_expiry");
        builder.Property(d => d.LatestDutyStatus).HasColumnName("latest_duty_status");
        builder.Property(d => d.LatestDrivingHours).HasColumnName("latest_driving_hours");
        builder.Property(d => d.LatestHosDate).HasColumnName("latest_hos_date");
    }
}
