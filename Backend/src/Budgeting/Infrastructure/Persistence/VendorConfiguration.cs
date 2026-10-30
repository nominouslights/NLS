using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Maps the Vendor aggregate to budgeting.vendors (snake_case columns).</summary>
public sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("vendors");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(v => v.TenantId).HasColumnName("tenant_id");
        builder.Property(v => v.Name).HasColumnName("name").HasMaxLength(Vendor.NameMaxLength);
        builder.Property(v => v.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(Vendor.NameMaxLength);
        builder.Property(v => v.ContactName)
            .HasColumnName("contact_name")
            .HasMaxLength(Vendor.ContactNameMaxLength);
        builder.Property(v => v.Email).HasColumnName("email").HasMaxLength(Vendor.EmailMaxLength);
        builder.Property(v => v.Phone).HasColumnName("phone").HasMaxLength(Vendor.PhoneMaxLength);
        builder.Property(v => v.Address).HasColumnName("address").HasMaxLength(Vendor.AddressMaxLength);
        builder.Property(v => v.Notes).HasColumnName("notes").HasMaxLength(Vendor.NotesMaxLength);
        builder.Property(v => v.GstRegistrationNumber)
            .HasColumnName("gst_registration_number")
            .HasMaxLength(Vendor.GstRegistrationNumberMaxLength);
        builder.Property(v => v.QboDisplayName)
            .HasColumnName("qbo_display_name")
            .HasMaxLength(Vendor.QboDisplayNameMaxLength);
        builder.Property(v => v.DefaultBudgetCode)
            .HasColumnName("default_budget_code")
            .HasMaxLength(Vendor.DefaultBudgetCodeMaxLength);

        builder.Property(v => v.IsActive).HasColumnName("is_active");
        builder.Property(v => v.CreatedBy).HasColumnName("created_by");
        builder.Property(v => v.ModifiedBy).HasColumnName("modified_by");
        builder.Property(v => v.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(v => v.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Race backstop for the handlers' case-insensitive name check: two near-simultaneous
        // creates (or a create racing a rename) can both pass the read-then-write check, and this
        // index kills the second write. normalized_name is already upper-cased, so a plain btree
        // unique index is the case-insensitive constraint — no citext, no expression index. It
        // also serves the handlers' lookup, which is an equality seek on exactly these columns.
        builder.HasIndex(v => new { v.TenantId, v.NormalizedName }).IsUnique();

        // DomainEvents ignore + Version concurrency token come from ModuleDbContext's
        // central aggregate conventions.
    }
}
