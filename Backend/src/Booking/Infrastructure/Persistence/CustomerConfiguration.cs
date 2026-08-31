using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Booking.Domain.Customers;

namespace NorthernLink.Booking.Infrastructure.Persistence;

/// <summary>
/// Maps the Customer aggregate to booking.customers (snake_case columns). phone_digits is
/// the digits-only comparison column the search matches against.
/// </summary>
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(32);
        builder.Property(c => c.PhoneDigits).HasColumnName("phone_digits").HasMaxLength(32);
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(200);
        builder.Property(c => c.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(c => new { c.TenantId, c.Name });
        builder.HasIndex(c => new { c.TenantId, c.PhoneDigits });
    }
}
