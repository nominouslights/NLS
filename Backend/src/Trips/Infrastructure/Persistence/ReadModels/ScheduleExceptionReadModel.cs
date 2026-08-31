using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of one schedule exception into <c>trips.rm_schedule_exceptions</c>,
/// rewritten wholesale from the owning template's aggregate on every exceptions change
/// (the <c>rm_shipment_legs</c> approach). <see cref="Kind"/> is the enum name.
/// <see cref="Version"/> is the owning aggregate's concurrency version at last projection.
/// </summary>
public sealed class ScheduleExceptionReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ScheduleTemplateId { get; set; }
    public DateOnly Date { get; set; }
    public string Kind { get; set; } = null!;
    public TimeOnly? DepartureTime { get; set; }
    public TimeOnly? ReturnDepartureTime { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class ScheduleExceptionReadModelConfiguration : IEntityTypeConfiguration<ScheduleExceptionReadModel>
{
    public void Configure(EntityTypeBuilder<ScheduleExceptionReadModel> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable("rm_schedule_exceptions", TripsServiceCollectionExtensions.SchemaName);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.ScheduleTemplateId).HasColumnName("schedule_template_id");
        builder.Property(e => e.Date).HasColumnName("date");
        builder.Property(e => e.Kind).HasColumnName("kind");
        builder.Property(e => e.DepartureTime).HasColumnName("departure_time");
        builder.Property(e => e.ReturnDepartureTime).HasColumnName("return_departure_time");
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(e => e.Version).HasColumnName("version");

        // The special-dates panel's query: one template's exceptions in date order.
        builder.HasIndex(e => new { e.TenantId, e.ScheduleTemplateId, e.Date });
    }
}
