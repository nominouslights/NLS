using NorthernLink.Fleet.Application.Maintenance.Completions.Log;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.WorkOrders;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class LogPmCompletionCommandHandlerTests
{
    private static LogPmCompletionCommand Command(
        Guid vehicleId,
        string code = "PM-E-001",
        PmEntryKind kind = PmEntryKind.Item,
        Guid? workOrderId = null,
        string? measurement = null,
        int odometerKm = 98_000) => new(
            TestVehicles.TenantId,
            vehicleId,
            code,
            kind,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3),
            odometerKm,
            PerformedBy: "R. Thomas",
            workOrderId,
            measurement,
            Notes: null);

    /// <summary>The Setup vehicle's current odometer (TestVehicles.Create default).</summary>
    private const int VehicleOdometerKm = 100_000;

    private static (LogPmCompletionCommandHandler Handler,
        InMemoryPmCompletionRepository Completions,
        InMemoryPlanAssignmentRepository Assignments,
        InMemoryMaintenancePlanRepository Plans,
        InMemoryWorkOrderRepository WorkOrders,
        MaintenancePlan Plan,
        Guid VehicleId) Setup(bool assigned = true, VehicleStatus? vehicleStatus = null, bool registerVehicle = true)
    {
        var completions = new InMemoryPmCompletionRepository();
        var assignments = new InMemoryPlanAssignmentRepository();
        var plans = new InMemoryMaintenancePlanRepository();
        var vehicles = new InMemoryVehicleRepository();
        var workOrders = new InMemoryWorkOrderRepository();
        var plan = TestMaintenancePlans.Create();
        plans.Plans.Add(plan);

        var vehicle = vehicleStatus is { } status ? TestVehicles.InStatus(status) : TestVehicles.Create();
        if (registerVehicle)
        {
            vehicles.Add(vehicle);
        }

        if (assigned)
        {
            assignments.Assignments.Add(
                PlanAssignment.Assign(TestVehicles.TenantId, vehicle.Id, plan.Id).Value);
        }

        var handler = new LogPmCompletionCommandHandler(completions, assignments, plans, vehicles, workOrders);
        return (handler, completions, assignments, plans, workOrders, plan, vehicle.Id);
    }

    [Fact]
    public async Task Logs_an_item_completion_with_the_plan_from_the_assignment()
    {
        var (handler, completions, _, _, _, plan, vehicleId) = Setup();

        var result = await handler.Handle(Command(vehicleId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(completions.Completions);
        Assert.Equal(result.Value, stored.Id);
        Assert.Equal(plan.Id, stored.PlanId);
        Assert.Equal("PM-E-001", stored.ItemCode);
        Assert.Equal(PmEntryKind.Item, stored.Kind);
        Assert.Equal(1, completions.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_vehicle_without_an_assignment_fails_with_not_found()
    {
        var (handler, completions, _, _, _, _, vehicleId) = Setup(assigned: false);

        var result = await handler.Handle(Command(vehicleId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.AssignmentNotFound, result.Error);
        Assert.Empty(completions.Completions);
    }

    [Fact]
    public async Task An_unknown_vehicle_fails_with_not_found()
    {
        var (handler, completions, _, _, _, _, vehicleId) = Setup(registerVehicle: false);

        var result = await handler.Handle(Command(vehicleId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.NotFound, result.Error);
        Assert.Empty(completions.Completions);
    }

    [Fact]
    public async Task A_disposed_vehicle_fails_with_conflict_the_ledger_is_closed()
    {
        var (handler, completions, _, _, _, _, vehicleId) = Setup(vehicleStatus: VehicleStatus.Sold);

        var result = await handler.Handle(Command(vehicleId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.Disposed, result.Error);
        Assert.Empty(completions.Completions);
    }

    [Fact]
    public async Task An_odometer_reading_at_the_plausibility_bound_is_accepted()
    {
        var (handler, completions, _, _, _, _, vehicleId) = Setup();

        var result = await handler.Handle(
            Command(vehicleId, odometerKm: VehicleOdometerKm + PmCompletion.MaxOdometerAheadKm),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(completions.Completions);
    }

    [Fact]
    public async Task An_odometer_reading_past_the_plausibility_bound_fails()
    {
        var (handler, completions, _, _, _, _, vehicleId) = Setup();

        var result = await handler.Handle(
            Command(vehicleId, odometerKm: VehicleOdometerKm + PmCompletion.MaxOdometerAheadKm + 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.OdometerImplausible(VehicleOdometerKm), result.Error);
        Assert.Empty(completions.Completions);
    }

    [Fact]
    public async Task An_odometer_reading_below_the_vehicles_is_legal_history()
    {
        var (handler, completions, _, _, _, _, vehicleId) = Setup();

        var result = await handler.Handle(
            Command(vehicleId, odometerKm: 10_000), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(completions.Completions);
    }

    [Fact]
    public async Task A_code_missing_from_the_plan_fails()
    {
        var (handler, completions, _, _, _, _, vehicleId) = Setup();

        var result = await handler.Handle(Command(vehicleId, code: "PM-B-999"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.CompletionCodeNotInPlan, result.Error);
        Assert.Empty(completions.Completions);
    }

    [Fact]
    public async Task An_overhaul_code_logged_as_an_item_fails_kinds_are_separate()
    {
        var (handler, _, _, _, _, _, vehicleId) = Setup();

        var result = await handler.Handle(
            Command(vehicleId, code: "OH-01", kind: PmEntryKind.Item), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.CompletionCodeNotInPlan, result.Error);
    }

    [Fact]
    public async Task An_overhaul_kind_validates_against_the_overhaul_codes()
    {
        var (handler, completions, _, _, _, _, vehicleId) = Setup();

        var result = await handler.Handle(
            Command(vehicleId, code: "OH-01", kind: PmEntryKind.Overhaul), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(completions.Completions);
        Assert.Equal(PmEntryKind.Overhaul, stored.Kind);
    }

    [Fact]
    public async Task The_code_is_trimmed_before_the_plan_lookup()
    {
        var (handler, completions, _, _, _, _, vehicleId) = Setup();

        var result = await handler.Handle(Command(vehicleId, code: " PM-E-001 "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PM-E-001", Assert.Single(completions.Completions).ItemCode);
    }

    [Fact]
    public async Task A_blank_code_fails_with_code_required()
    {
        var (handler, _, _, _, _, _, vehicleId) = Setup();

        var result = await handler.Handle(Command(vehicleId, code: "  "), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.CompletionCodeRequired, result.Error);
    }

    [Fact]
    public async Task An_unknown_work_order_fails_with_not_found()
    {
        var (handler, completions, _, _, _, _, vehicleId) = Setup();

        var result = await handler.Handle(
            Command(vehicleId, workOrderId: Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkOrderErrors.NotFound, result.Error);
        Assert.Empty(completions.Completions);
    }

    [Fact]
    public async Task A_work_order_belonging_to_another_vehicle_fails()
    {
        var (handler, completions, _, _, workOrders, _, vehicleId) = Setup();
        // A real work order — but raised against a different unit entirely.
        var foreignWorkOrder = CreateWorkOrder(vehicleId: Guid.NewGuid());
        workOrders.Add(foreignWorkOrder);

        var result = await handler.Handle(
            Command(vehicleId, workOrderId: foreignWorkOrder.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.CompletionWorkOrderNotForVehicle, result.Error);
        Assert.Empty(completions.Completions);
    }

    [Fact]
    public async Task A_known_work_order_links_to_the_completion()
    {
        var (handler, completions, _, _, workOrders, _, vehicleId) = Setup();
        var workOrder = WorkOrder.Create(
            TestVehicles.TenantId,
            vehicleId,
            "WO-1",
            "PM shop visit",
            "Certify due PM items",
            WorkOrderPriority.Medium,
            WorkOrderSource.PmReminder,
            sourceRef: null,
            createdBy: "Dispatch",
            assignedTo: null,
            dueDate: null,
            lineItems: ["PM-E-001 — Engine oil & filter"],
            shopId: null,
            authorizedLimitCad: null,
            budgetCode: null,
            dateRequiredOrOos: null);
        Assert.True(workOrder.IsSuccess, $"Test work order creation failed: {workOrder.Error.Code}");
        workOrders.Add(workOrder.Value);

        var result = await handler.Handle(
            Command(vehicleId, workOrderId: workOrder.Value.Id, measurement: "5.5 mm pads"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(completions.Completions);
        Assert.Equal(workOrder.Value.Id, stored.WorkOrderId);
        Assert.Equal("5.5 mm pads", stored.Measurement);
    }

    private static WorkOrder CreateWorkOrder(Guid vehicleId)
    {
        var workOrder = WorkOrder.Create(
            TestVehicles.TenantId,
            vehicleId,
            "WO-2",
            "PM shop visit",
            "Certify due PM items",
            WorkOrderPriority.Medium,
            WorkOrderSource.PmReminder,
            sourceRef: null,
            createdBy: "Dispatch",
            assignedTo: null,
            dueDate: null,
            lineItems: ["PM-E-001 — Engine oil & filter"],
            shopId: null,
            authorizedLimitCad: null,
            budgetCode: null,
            dateRequiredOrOos: null);
        Assert.True(workOrder.IsSuccess, $"Test work order creation failed: {workOrder.Error.Code}");
        return workOrder.Value;
    }
}
