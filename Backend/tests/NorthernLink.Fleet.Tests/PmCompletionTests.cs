using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Maintenance.Events;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class PmCompletionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();

    private static Result<PmCompletion> Log(
        string itemCode = "PM-E-001",
        DateOnly? performedAt = null,
        int odometerKm = 42_000,
        string performedBy = "Northern Link Shop",
        Guid? vehicleId = null,
        Guid? planId = null,
        string? measurement = "Pad thickness 6 mm",
        string? notes = null) => PmCompletion.Log(
            TenantId,
            vehicleId ?? VehicleId,
            planId ?? PlanId,
            itemCode,
            PmEntryKind.Item,
            performedAt ?? new DateOnly(2026, 8, 22),
            odometerKm,
            performedBy,
            workOrderId: null,
            measurement,
            notes);

    [Fact]
    public void Log_records_the_completion_and_raises_the_logged_event()
    {
        var result = Log();

        Assert.True(result.IsSuccess);
        var completion = result.Value;
        Assert.Equal(TenantId, completion.TenantId);
        Assert.Equal(VehicleId, completion.VehicleId);
        Assert.Equal(PlanId, completion.PlanId);
        Assert.Equal("PM-E-001", completion.ItemCode);
        Assert.Equal(PmEntryKind.Item, completion.Kind);
        Assert.Equal(new DateOnly(2026, 8, 22), completion.PerformedAt);
        Assert.Equal(42_000, completion.OdometerKm);
        Assert.Equal("Northern Link Shop", completion.PerformedBy);
        Assert.Equal("Pad thickness 6 mm", completion.Measurement);
        var evt = Assert.IsType<PmCompletionLoggedDomainEvent>(Assert.Single(completion.DomainEvents));
        Assert.Equal(completion.Id, evt.CompletionId);
        Assert.Equal(VehicleId, evt.VehicleId);
    }

    [Fact]
    public void Log_rejects_an_empty_code()
    {
        var result = Log(itemCode: " ");

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.CompletionCodeRequired, result.Error);
    }

    [Fact]
    public void Log_rejects_an_empty_performed_by()
    {
        var result = Log(performedBy: "");

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.PerformedByRequired, result.Error);
    }

    [Fact]
    public void Log_rejects_a_negative_odometer()
    {
        var result = Log(odometerKm: -1);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.InvalidOdometer, result.Error);
    }

    [Fact]
    public void Log_rejects_a_default_performed_date()
    {
        var result = Log(performedAt: default(DateOnly));

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.PerformedAtRequired, result.Error);
    }

    [Fact]
    public void Log_rejects_an_empty_vehicle_id()
    {
        var result = Log(vehicleId: Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.VehicleRequired, result.Error);
    }

    [Fact]
    public void Log_rejects_an_empty_plan_id()
    {
        var result = Log(planId: Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.PlanRequired, result.Error);
    }

    [Fact]
    public void Log_rejects_a_performed_date_more_than_a_day_in_the_future()
    {
        var result = Log(performedAt: DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime).AddDays(2));

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.PerformedAtInFuture, result.Error);
    }

    [Fact]
    public void Log_accepts_a_performed_date_of_tomorrow()
    {
        // One day of grace absorbs a shop clock running a timezone ahead of UTC.
        var result = Log(performedAt: DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime).AddDays(1));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Log_trims_the_item_code_to_match_the_plan_side_natural_key()
    {
        var result = Log(itemCode: " PM-E-001 ");

        Assert.True(result.IsSuccess);
        Assert.Equal("PM-E-001", result.Value.ItemCode);
    }

    [Fact]
    public void Log_rejects_an_item_code_over_the_cap()
    {
        var result = Log(itemCode: new string('x', MaintenancePlan.CodeMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.CompletionCodeTooLong, result.Error);
    }

    [Fact]
    public void Log_rejects_a_performed_by_over_the_cap()
    {
        var result = Log(performedBy: new string('x', PmCompletion.PerformedByMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.PerformedByTooLong, result.Error);
    }

    [Fact]
    public void Log_rejects_a_measurement_over_the_cap()
    {
        var result = Log(measurement: new string('x', PmCompletion.MeasurementMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.MeasurementTooLong, result.Error);
    }

    [Fact]
    public void Log_rejects_notes_over_the_cap()
    {
        var result = Log(notes: new string('x', PmCompletion.NotesMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.CompletionNotesTooLong, result.Error);
    }
}
