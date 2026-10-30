using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.BookeoImports;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// trips.bookeo_import_batches — one row per uploaded Bookeo report. <c>parsed_rows</c> and
/// <c>summary</c> are jsonb written by the application (never the raw file, never a tax figure).
/// <c>committed_at_utc</c> is a concurrency token: the commit's UPDATE carries
/// "AND committed_at_utc IS NULL", so two simultaneous confirms of one preview cannot both apply.
/// </summary>
public sealed class BookeoImportBatchConfiguration : IEntityTypeConfiguration<BookeoImportBatch>
{
    public void Configure(EntityTypeBuilder<BookeoImportBatch> builder)
    {
        builder.ToTable("bookeo_import_batches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.TenantId).HasColumnName("tenant_id");
        builder.Property(b => b.FileName).HasColumnName("file_name").HasMaxLength(260);
        builder.Property(b => b.UploadedBy).HasColumnName("uploaded_by").HasMaxLength(256);
        builder.Property(b => b.UploadedAtUtc).HasColumnName("uploaded_at_utc");
        builder.Property(b => b.ParsedRowsJson).HasColumnName("parsed_rows").HasColumnType("jsonb");
        builder.Property(b => b.PlanHash).HasColumnName("plan_hash").HasMaxLength(64);
        builder.Property(b => b.SummaryJson).HasColumnName("summary").HasColumnType("jsonb");
        builder.Property(b => b.CommittedAtUtc).HasColumnName("committed_at_utc").IsConcurrencyToken();
        builder.Property(b => b.CommittedBy).HasColumnName("committed_by").HasMaxLength(256);
        builder.Ignore(b => b.IsCommitted);

        builder.HasIndex(b => new { b.TenantId, b.UploadedAtUtc });
    }
}

/// <summary>
/// trips.bookeo_bookings — the import ledger. (tenant_id, booking_number) is unique: the
/// DB-atomic idempotency key a racing commit trips over (23505 → PreviewStale).
/// </summary>
public sealed class BookeoBookingConfiguration : IEntityTypeConfiguration<BookeoBooking>
{
    public void Configure(EntityTypeBuilder<BookeoBooking> builder)
    {
        builder.ToTable("bookeo_bookings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.TenantId).HasColumnName("tenant_id");
        builder.Property(b => b.BookingNumber).HasColumnName("booking_number").HasMaxLength(32);
        builder.Property(b => b.ProductCode).HasColumnName("product_code").HasMaxLength(64);
        builder.Property(b => b.ProductName).HasColumnName("product_name").HasMaxLength(200);
        builder.Property(b => b.Destination).HasColumnName("destination").HasMaxLength(200);
        builder.Property(b => b.ServiceDate).HasColumnName("service_date");
        builder.Property(b => b.WindowStart).HasColumnName("window_start");
        builder.Property(b => b.WindowEnd).HasColumnName("window_end");
        builder.Property(b => b.BookeoStatus).HasColumnName("bookeo_status").HasMaxLength(64);
        builder.Property(b => b.Participants).HasColumnName("participants");
        builder.OwnsMany(b => b.Passengers, passenger => passenger.ToJson("passengers"));
        builder.Property(b => b.CustomerName).HasColumnName("customer_name").HasMaxLength(200);
        builder.Property(b => b.CustomerEmail).HasColumnName("customer_email").HasMaxLength(256);
        builder.Property(b => b.CustomerPhone).HasColumnName("customer_phone").HasMaxLength(64);
        builder.Property(b => b.TotalGrossCad).HasColumnName("total_gross_cad").HasPrecision(12, 2);
        builder.Property(b => b.TotalPaidCad).HasColumnName("total_paid_cad").HasPrecision(12, 2);
        builder.Property(b => b.TotalDueCad).HasColumnName("total_due_cad").HasPrecision(12, 2);
        builder.Property(b => b.UnitText).HasColumnName("unit_text").HasMaxLength(128);
        builder.Property(b => b.TripId).HasColumnName("trip_id");
        builder.Property(b => b.ContentHash).HasColumnName("content_hash").HasMaxLength(64);
        builder.Property(b => b.FirstImportedAtUtc).HasColumnName("first_imported_at_utc");
        builder.Property(b => b.LastImportedAtUtc).HasColumnName("last_imported_at_utc");
        builder.Property(b => b.LastBatchId).HasColumnName("last_batch_id");

        builder.HasIndex(b => new { b.TenantId, b.BookingNumber }).IsUnique();
        builder.HasIndex(b => new { b.TenantId, b.TripId });
    }
}

/// <summary>
/// trips.bookeo_product_mappings. The per-tenant uniqueness is on
/// (tenant_id, product_code, lower(coalesce(destination, ''))) — an expression index EF cannot
/// model, so the migration creates it in hand-written SQL.
/// </summary>
public sealed class BookeoProductMappingConfiguration : IEntityTypeConfiguration<BookeoProductMapping>
{
    public void Configure(EntityTypeBuilder<BookeoProductMapping> builder)
    {
        builder.ToTable("bookeo_product_mappings");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.TenantId).HasColumnName("tenant_id");
        builder.Property(m => m.ProductCode).HasColumnName("product_code").HasMaxLength(64);
        builder.Property(m => m.ProductName).HasColumnName("product_name").HasMaxLength(200);
        builder.Property(m => m.Destination).HasColumnName("destination").HasMaxLength(200);
        builder.Property(m => m.RouteId).HasColumnName("route_id");
        builder.Property(m => m.Direction).HasColumnName("direction").HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.ResidentStopRole)
            .HasColumnName("resident_stop_role")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(m => m.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(m => new { m.TenantId, m.ProductCode });
    }
}

/// <summary>trips.bookeo_unit_mappings — (tenant_id, unit_text) unique; unit_text is stored normalized.</summary>
public sealed class BookeoUnitMappingConfiguration : IEntityTypeConfiguration<BookeoUnitMapping>
{
    public void Configure(EntityTypeBuilder<BookeoUnitMapping> builder)
    {
        builder.ToTable("bookeo_unit_mappings");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.TenantId).HasColumnName("tenant_id");
        builder.Property(m => m.UnitText).HasColumnName("unit_text").HasMaxLength(128);
        builder.Property(m => m.VehicleId).HasColumnName("vehicle_id");
        builder.Property(m => m.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(m => new { m.TenantId, m.UnitText }).IsUnique();
    }
}
