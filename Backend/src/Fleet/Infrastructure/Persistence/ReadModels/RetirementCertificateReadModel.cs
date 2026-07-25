using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a retirement certificate, via <c>fleet.mv_retirement_certificates</c>
/// / <c>fleet.v_retirement_certificates</c>. Unlike the other read models this carries no
/// <c>version</c>: <c>RetirementCertificate</c> is a plain <c>Entity</c> (no optimistic-concurrency
/// column) and is immutable once issued, so it has no "outdated vs aggregate" state.
/// </summary>
public sealed class RetirementCertificateReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VehicleId { get; set; }
    public string CertificateNumber { get; set; } = null!;
    public string Vin { get; set; } = null!;
    public string UnitNumber { get; set; } = null!;
    public string Make { get; set; } = null!;
    public string Model { get; set; } = null!;
    public int Year { get; set; }
    public int FinalOdometerKm { get; set; }
    public string RetirementReason { get; set; } = null!;
    public DateTimeOffset RetiredAtUtc { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
}

public sealed class RetirementCertificateReadModelConfiguration : IEntityTypeConfiguration<RetirementCertificateReadModel>
{
    public void Configure(EntityTypeBuilder<RetirementCertificateReadModel> builder)
    {
        builder.HasKey(c => c.Id);
        builder.ToTable("rm_retirement_certificates", FleetServiceCollectionExtensions.SchemaName);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.VehicleId).HasColumnName("vehicle_id");
        builder.Property(c => c.CertificateNumber).HasColumnName("certificate_number");
        builder.Property(c => c.Vin).HasColumnName("vin");
        builder.Property(c => c.UnitNumber).HasColumnName("unit_number");
        builder.Property(c => c.Make).HasColumnName("make");
        builder.Property(c => c.Model).HasColumnName("model");
        builder.Property(c => c.Year).HasColumnName("year");
        builder.Property(c => c.FinalOdometerKm).HasColumnName("final_odometer_km");
        builder.Property(c => c.RetirementReason).HasColumnName("retirement_reason");
        builder.Property(c => c.RetiredAtUtc).HasColumnName("retired_at_utc");
        builder.Property(c => c.IssuedAtUtc).HasColumnName("issued_at_utc");
    }
}
