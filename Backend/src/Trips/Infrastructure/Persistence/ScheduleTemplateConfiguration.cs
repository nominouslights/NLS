using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.Schedules;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Maps the ScheduleTemplate aggregate to trips.schedule_templates (snake_case
/// columns). DaysOfWeek persists as a jsonb array of day names ("Monday", …) — stable
/// strings rather than integers, matching the platform's enum-as-string convention.
/// </summary>
public sealed class ScheduleTemplateConfiguration : IEntityTypeConfiguration<ScheduleTemplate>
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Default;

    public void Configure(EntityTypeBuilder<ScheduleTemplate> builder)
    {
        builder.ToTable("schedule_templates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.TenantId).HasColumnName("tenant_id");
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(t => t.RouteId).HasColumnName("route_id");
        builder.Property(t => t.ServiceType)
            .HasColumnName("service_type")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(t => t.ClientId).HasColumnName("client_id");
        builder.Property(t => t.ClientName).HasColumnName("client_name").HasMaxLength(200);

        builder.Property(t => t.RecurrenceKind)
            .HasColumnName("recurrence_kind")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(t => t.DaysOfWeek)
            .HasColumnName("days_of_week")
            .HasColumnType("jsonb")
            .HasConversion(
                days => JsonSerializer.Serialize(days.Select(day => day.ToString()).ToList(), JsonOptions),
                json => JsonSerializer.Deserialize<List<string>>(json, JsonOptions)!
                    .Select(name => Enum.Parse<DayOfWeek>(name))
                    .ToList(),
                new ValueComparer<List<DayOfWeek>>(
                    (left, right) => left!.SequenceEqual(right!),
                    days => days.Aggregate(0, (hash, day) => HashCode.Combine(hash, day)),
                    days => days.ToList()));

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
        builder.Property(t => t.DefaultVehicleUnit).HasColumnName("default_vehicle_unit").HasMaxLength(32);
        builder.Property(t => t.DefaultDriverId).HasColumnName("default_driver_id");
        builder.Property(t => t.GenerationHorizonDays).HasColumnName("generation_horizon_days");
        builder.Property(t => t.CutoffNote).HasColumnName("cutoff_note").HasMaxLength(500);
        builder.Property(t => t.Active).HasColumnName("active");
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(t => t.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Read-only navigation over the aggregate's backing field — exceptions only ever
        // change through ScheduleTemplate, which owns the one-per-date and time invariants.
        builder.HasMany(t => t.Exceptions)
            .WithOne()
            .HasForeignKey(e => e.ScheduleTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(ScheduleTemplate.Exceptions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(t => new { t.TenantId, t.Active });
        builder.HasIndex(t => new { t.TenantId, t.RouteId });
    }
}

/// <summary>
/// Maps <see cref="ScheduleException"/> to trips.schedule_exceptions. Carries its own
/// <c>tenant_id</c> and its own RLS policy — isolation is never inherited through a
/// foreign key. The unique (tenant, template, date) index backstops the aggregate's
/// one-exception-per-date invariant against concurrent writers.
/// </summary>
public sealed class ScheduleExceptionConfiguration : IEntityTypeConfiguration<ScheduleException>
{
    public void Configure(EntityTypeBuilder<ScheduleException> builder)
    {
        builder.ToTable("schedule_exceptions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.ScheduleTemplateId).HasColumnName("schedule_template_id");
        builder.Property(e => e.Date).HasColumnName("date");

        builder.Property(e => e.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(e => e.DepartureTime).HasColumnName("departure_time");
        builder.Property(e => e.ReturnDepartureTime).HasColumnName("return_departure_time");
        builder.Property(e => e.Note).HasColumnName("note").HasMaxLength(500);
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(e => new { e.TenantId, e.ScheduleTemplateId, e.Date }).IsUnique();
    }
}
