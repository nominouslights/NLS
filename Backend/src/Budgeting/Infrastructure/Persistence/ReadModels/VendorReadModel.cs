using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a vendor into <c>budgeting.rm_vendors</c> — the row the vendor
/// register lists. A mutable class rather than a record, like every read model on the platform
/// (see <see cref="BudgetCodeReadModel"/>); the wire contract is
/// <see cref="Application.Vendors.VendorResponse"/>.
/// <para>
/// <see cref="NormalizedName"/> is carried for ordering only. Its index here is deliberately
/// <b>not</b> unique: uniqueness is the write table's job, and a projection batch that applies a
/// swap of two names one row at a time must not trip over its own intermediate state.
/// </para>
/// </summary>
public sealed class VendorReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string NormalizedName { get; set; } = null!;
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public string? GstRegistrationNumber { get; set; }
    public string? QboDisplayName { get; set; }
    public string? DefaultBudgetCode { get; set; }
    public bool IsActive { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

/// <summary>Maps <see cref="VendorReadModel"/> to budgeting.rm_vendors.</summary>
public sealed class VendorReadModelConfiguration : IEntityTypeConfiguration<VendorReadModel>
{
    public void Configure(EntityTypeBuilder<VendorReadModel> builder)
    {
        builder.HasKey(v => v.Id);
        builder.ToTable("rm_vendors", BudgetingServiceCollectionExtensions.SchemaName);

        builder.Property(v => v.Id).HasColumnName("id");
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
        builder.Property(v => v.Version).HasColumnName("version");

        // Serves the register list: the tenant's rows in name order.
        builder.HasIndex(v => new { v.TenantId, v.NormalizedName });
    }
}
