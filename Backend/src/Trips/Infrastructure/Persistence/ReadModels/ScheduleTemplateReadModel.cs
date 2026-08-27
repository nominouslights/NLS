using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read-side projection of a schedule template into <c>trips.rm_schedule_templates</c>.
/// <see cref="RouteName"/> is resolved from the routes table at projection time (a
/// same-schema denormalization for the RoutesSchedules screen); days are carried as day
/// names in a text[] (the response contract is strings). <see cref="Version"/> is the
/// aggregate's concurrency version at last projection.
/// </summary>
public sealed class ScheduleTemplateReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public Guid RouteId { get; set; }
    public string? RouteName { get; set; }
    public string ServiceType { get; set; } = null!;
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string RecurrenceKind { get; set; } = null!;
    public List<string> DaysOfWeek { get; set; } = [];
    public int? IntervalDays { get; set; }
    public DateOnly? AnchorDate { get; set; }
    public List<int> DaysOfMonth { get; set; } = [];
    public TimeOnly DepartureTime { get; set; }
    public TimeOnly? ReturnDepartureTime { get; set; }
    public bool ReturnNextDay { get; set; }
    public int SeatsCapacity { get; set; }
    public int? SeatsMinimum { get; set; }
    public string? DefaultVehicleUnit { get; set; }
    public Guid? DefaultDriverId { get; set; }
    public int GenerationHorizonDays { get; set; }
    public string? CutoffNote { get; set; }
    public bool Active { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}

public sealed class ScheduleTemplateReadModelConfiguration : IEntityTypeConfiguration<ScheduleTemplateReadModel>
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Default;

    public void Configure(EntityTypeBuilder<ScheduleTemplateReadModel> builder)
    {
        builder.HasKey(t => t.Id);
        builder.ToTable("rm_schedule_templates", TripsServiceCollectionExtensions.SchemaName);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.Name).HasColumnName("name");
        builder.Property(t => t.RouteId).HasColumnName("route_id");
        builder.Property(t => t.RouteName).HasColumnName("route_name");
        builder.Property(t => t.ServiceType).HasColumnName("service_type");
        builder.Property(t => t.ClientId).HasColumnName("client_id");
        builder.Property(t => t.ClientName).HasColumnName("client_name");
        builder.Property(t => t.RecurrenceKind).HasColumnName("recurrence_kind");
        builder.PrimitiveCollection(t => t.DaysOfWeek).HasColumnName("days_of_week");
        builder.Property(t => t.IntervalDays).HasColumnName("interval_days");
        builder.Property(t => t.AnchorDate).HasColumnName("anchor_date");
        builder.Property(t => t.DaysOfMonth)
            .HasColumnName("days_of_month")
            .HasColumnType("jsonb")
            .HasConversion(
                days => JsonSerializer.Serialize(days, JsonOptions),
                json => JsonSerializer.Deserialize<List<int>>(json, JsonOptions)!,
                new ValueComparer<List<int>>(
                    (left, right) => left!.SequenceEqual(right!),
                    days => days.Aggregate(0, (hash, day) => HashCode.Combine(hash, day)),
                    days => days.ToList()));
        builder.Property(t => t.DepartureTime).HasColumnName("departure_time");
        builder.Property(t => t.ReturnDepartureTime).HasColumnName("return_departure_time");
        builder.Property(t => t.ReturnNextDay).HasColumnName("return_next_day");
        builder.Property(t => t.SeatsCapacity).HasColumnName("seats_capacity");
        builder.Property(t => t.SeatsMinimum).HasColumnName("seats_minimum");
        builder.Property(t => t.DefaultVehicleUnit).HasColumnName("default_vehicle_unit");
        builder.Property(t => t.DefaultDriverId).HasColumnName("default_driver_id");
        builder.Property(t => t.GenerationHorizonDays).HasColumnName("generation_horizon_days");
        builder.Property(t => t.CutoffNote).HasColumnName("cutoff_note");
        builder.Property(t => t.Active).HasColumnName("active");
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(t => t.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(t => t.Version).HasColumnName("version");
    }
}
