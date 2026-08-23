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
        string performedBy = "Northern Link Shop") => PmCompletion.Log(
            TenantId,
            VehicleId,
            PlanId,
            itemCode,
            PmEntryKind.Item,
            performedAt ?? new DateOnly(2026, 8, 22),
            odometerKm,
            performedBy,
            workOrderId: null,
            measurement: "Pad thickness 6 mm",
            notes: null);

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
}
