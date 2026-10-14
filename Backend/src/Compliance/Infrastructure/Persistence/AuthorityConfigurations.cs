using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Compliance.Application.Integration;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Insurance;
using NorthernLink.Compliance.Domain.Registrations;
using NorthernLink.Compliance.Domain.Signatures;

namespace NorthernLink.Compliance.Infrastructure.Persistence;

/// <summary>Maps CarrierAuthority to compliance.carrier_authorities — one row per tenant; certificates and the signature as jsonb.</summary>
public sealed class CarrierAuthorityConfiguration : IEntityTypeConfiguration<CarrierAuthority>
{
    public const string TenantIndex = "IX_carrier_authorities_tenant_id";

    public void Configure(EntityTypeBuilder<CarrierAuthority> builder)
    {
        builder.ToTable("carrier_authorities");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.TenantId).HasColumnName("tenant_id");
        builder.Property(a => a.NscNumber).HasColumnName("nsc_number").HasMaxLength(CarrierAuthority.NscNumberMaxLength);
        builder.Property(a => a.CurrentSfc).HasColumnName("current_sfc").HasJsonb().IsRequired(false);
        builder.Property(a => a.SfcHistory).HasColumnName("sfc_history").HasJsonb();
        builder.Property(a => a.SafetyRating).HasColumnName("safety_rating").HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.SafetyRatingAsOf).HasColumnName("safety_rating_as_of");
        builder.Property(a => a.IntraProvincial).HasColumnName("intra_provincial");
        builder.Property(a => a.IntraProvincialDeclaredOn).HasColumnName("intra_provincial_declared_on");
        builder.Property(a => a.IntraProvincialSignature).HasColumnName("intra_provincial_signature").HasJsonb().IsRequired(false);
        builder.Property(a => a.OperatingRoutes).HasColumnName("operating_routes").HasMaxLength(CarrierAuthority.OperatingRoutesMaxLength);
        builder.Property(a => a.ModifiedBy).HasColumnName("modified_by");
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(a => a.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // One authority per tenant.
        builder.HasIndex(a => a.TenantId).IsUnique().HasDatabaseName(TenantIndex);
    }
}

/// <summary>
/// Maps InsurancePolicy to compliance.insurance_policies. The one-Current-per-vehicle rule is a
/// partial unique index over (tenant_id, vehicle_id, status) WHERE status = 'Current'. Status is
/// in the key columns on purpose: a renewal updates the old row's status and inserts the new row
/// in one SaveChanges, and with status as an index column EF sees the old row releasing the
/// (tenant, vehicle, Current) value and orders that UPDATE before the INSERT — with only
/// (tenant_id, vehicle_id) it could emit the INSERT first and trip the index.
/// </summary>
public sealed class InsurancePolicyConfiguration : IEntityTypeConfiguration<InsurancePolicy>
{
    public const string OneCurrentIndex = "IX_insurance_policies_one_current_per_vehicle";

    public void Configure(EntityTypeBuilder<InsurancePolicy> builder)
    {
        builder.ToTable("insurance_policies");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.VehicleId).HasColumnName("vehicle_id");
        builder.Property(p => p.VehicleUnitNumber).HasColumnName("vehicle_unit_number").HasMaxLength(InsurancePolicy.UnitNumberMaxLength);
        builder.Property(p => p.Insurer).HasColumnName("insurer").HasMaxLength(InsurancePolicy.InsurerMaxLength);
        builder.Property(p => p.PolicyNumber).HasColumnName("policy_number").HasMaxLength(InsurancePolicy.PolicyNumberMaxLength);
        builder.Property(p => p.LiabilityLimitCad).HasColumnName("liability_limit_cad").HasPrecision(14, 2);
        builder.Property(p => p.CargoCoverageCad).HasColumnName("cargo_coverage_cad").HasPrecision(14, 2);
        builder.Property(p => p.MinimumLiabilityCad).HasColumnName("minimum_liability_cad").HasPrecision(14, 2);
        builder.Property(p => p.EffectiveOn).HasColumnName("effective_on");
        builder.Property(p => p.ExpiresOn).HasColumnName("expires_on");
        builder.Property(p => p.Certificate).HasColumnName("certificate").HasJsonb().IsRequired(false);
        builder.Property(p => p.FilingConfirmation).HasColumnName("filing_confirmation").HasJsonb().IsRequired(false);
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.SupersedesId).HasColumnName("supersedes_id");
        builder.Property(p => p.SupersededById).HasColumnName("superseded_by_id");
        builder.Property(p => p.SupersededOn).HasColumnName("superseded_on");
        builder.Property(p => p.ComplianceRecordId).HasColumnName("compliance_record_id");
        builder.Property(p => p.DocumentNumber).HasColumnName("document_number").HasMaxLength(Domain.Numbering.DocumentNumberFormat.MaxLength);
        builder.Property(p => p.ModifiedBy).HasColumnName("modified_by");
        builder.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(p => p.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Ignore(p => p.IsBelowMinimum);
        builder.Ignore(p => p.SourceRecordId);

        builder.HasIndex(p => new { p.TenantId, p.VehicleId, p.Status })
            .IsUnique()
            .HasFilter("status = 'Current'")
            .HasDatabaseName(OneCurrentIndex);
        builder.HasIndex(p => new { p.TenantId, p.ExpiresOn });
    }
}

/// <summary>Maps VehicleRegistration to compliance.vehicle_registrations — same one-Current shape as insurance.</summary>
public sealed class VehicleRegistrationConfiguration : IEntityTypeConfiguration<VehicleRegistration>
{
    public const string OneCurrentIndex = "IX_vehicle_registrations_one_current_per_vehicle";

    public void Configure(EntityTypeBuilder<VehicleRegistration> builder)
    {
        builder.ToTable("vehicle_registrations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.VehicleId).HasColumnName("vehicle_id");
        builder.Property(r => r.VehicleUnitNumber).HasColumnName("vehicle_unit_number").HasMaxLength(VehicleRegistration.UnitNumberMaxLength);
        builder.Property(r => r.Plate).HasColumnName("plate").HasMaxLength(VehicleRegistration.PlateMaxLength);
        builder.Property(r => r.VehicleClass).HasColumnName("vehicle_class").HasMaxLength(VehicleRegistration.VehicleClassMaxLength);
        builder.Property(r => r.TermMonths).HasColumnName("term_months");
        builder.Property(r => r.EffectiveOn).HasColumnName("effective_on");
        builder.Property(r => r.ExpiresOn).HasColumnName("expires_on");
        builder.Property(r => r.Document).HasColumnName("document").HasJsonb().IsRequired(false);
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.SupersedesId).HasColumnName("supersedes_id");
        builder.Property(r => r.SupersededById).HasColumnName("superseded_by_id");
        builder.Property(r => r.SupersededOn).HasColumnName("superseded_on");
        builder.Property(r => r.ComplianceRecordId).HasColumnName("compliance_record_id");
        builder.Property(r => r.DocumentNumber).HasColumnName("document_number").HasMaxLength(Domain.Numbering.DocumentNumberFormat.MaxLength);
        builder.Property(r => r.ModifiedBy).HasColumnName("modified_by");
        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Ignore(r => r.SourceRecordId);

        builder.HasIndex(r => new { r.TenantId, r.VehicleId, r.Status })
            .IsUnique()
            .HasFilter("status = 'Current'")
            .HasDatabaseName(OneCurrentIndex);
        builder.HasIndex(r => new { r.TenantId, r.ExpiresOn });
    }
}

/// <summary>compliance.vehicle_lookup — the Fleet replica plus the Compliance-only regulated override. Plain keyed rows.</summary>
public sealed class VehicleLookupConfiguration : IEntityTypeConfiguration<VehicleLookup>
{
    public void Configure(EntityTypeBuilder<VehicleLookup> builder)
    {
        builder.ToTable("vehicle_lookup");

        builder.HasKey(v => v.VehicleId);
        builder.Property(v => v.VehicleId).HasColumnName("vehicle_id").ValueGeneratedNever();
        builder.Property(v => v.TenantId).HasColumnName("tenant_id");

        // Widths match Trips' replica of the same event (fleet's source columns).
        builder.Property(v => v.UnitNumber).HasColumnName("unit_number").HasMaxLength(32);
        builder.Property(v => v.Status).HasColumnName("status").HasMaxLength(16);
        builder.Property(v => v.SeatingCapacity).HasColumnName("seating_capacity");
        builder.Property(v => v.RequiredLicenceClass).HasColumnName("required_licence_class").HasMaxLength(16);
        builder.Property(v => v.RegulatedOverride).HasColumnName("regulated_override");
        builder.Property(v => v.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Ignore(v => v.CountsForReadiness);

        builder.HasIndex(v => v.TenantId);
    }
}

/// <summary>compliance.driver_lookup — the Drivers replica.</summary>
public sealed class DriverLookupConfiguration : IEntityTypeConfiguration<DriverLookup>
{
    public void Configure(EntityTypeBuilder<DriverLookup> builder)
    {
        builder.ToTable("driver_lookup");

        builder.HasKey(d => d.DriverId);
        builder.Property(d => d.DriverId).HasColumnName("driver_id").ValueGeneratedNever();
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(128);
        builder.Property(d => d.Status).HasColumnName("status").HasMaxLength(16);
        builder.Property(d => d.UserId).HasColumnName("user_id");
        builder.Property(d => d.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(d => d.TenantId);
        builder.HasIndex(d => d.UserId).HasFilter("user_id IS NOT NULL");
    }
}

/// <summary>compliance.user_lookup — the Identity account replica.</summary>
public sealed class UserLookupConfiguration : IEntityTypeConfiguration<UserLookup>
{
    public void Configure(EntityTypeBuilder<UserLookup> builder)
    {
        builder.ToTable("user_lookup");

        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(u => u.TenantId).HasColumnName("tenant_id");

        // 256 / 128 match identity.users so the replica never truncates its source.
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(256);
        builder.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(128);
        builder.Property(u => u.Role).HasColumnName("role").HasMaxLength(32);
        builder.Property(u => u.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(u => u.TenantId);
    }
}
