using NorthernLink.Fleet.Application.WorkOrders.Complete;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Services;
using NorthernLink.Fleet.Domain.WorkOrders;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// Completing a work order logs the resolving service, closes the order — and clears the defects
/// of the DVIR that generated it. Completion is the only point that asserts a mechanic actually
/// touched the truck: creating or starting a work order leaves every defect open.
/// </summary>
public class CompleteWorkOrderCommandHandlerTests
{
    private static CompleteWorkOrderCommand Command(Guid workOrderId, string performedBy = "M. Cardinal") =>
        new(
            TestVehicles.TenantId,
            workOrderId,
            Date: DateTimeOffset.UtcNow,
            PerformedBy: performedBy,
            Category: ServiceCategory.InspectionFix,
            OdometerKm: 118_500,
            ItemsChanged: ["Brake line"],
            Reason: "DVIR defect",
            PartsUsed: [],
            LaborHours: 2.5m,
            CostCad: 410m,
            Notes: null);

    private static (CompleteWorkOrderCommandHandler Handler,
        InMemoryWorkOrderRepository WorkOrders,
        InMemoryServiceRecordRepository Services,
        InMemoryVehicleInspectionRepository Inspections) Setup()
    {
        var workOrders = new InMemoryWorkOrderRepository();
        var services = new InMemoryServiceRecordRepository();
        var inspections = new InMemoryVehicleInspectionRepository();

        return (new CompleteWorkOrderCommandHandler(workOrders, services, inspections),
            workOrders, services, inspections);
    }

    private static WorkOrder OpenWorkOrder(Guid vehicleId, IReadOnlyList<string>? lineItems = null) =>
        WorkOrder.Create(
            TestVehicles.TenantId,
            vehicleId,
            // Number is varchar(16) on the aggregate table — keep test numbers short.
            number: "WO-T1",
            title: "Replace brake line",
            description: null,
            WorkOrderPriority.High,
            WorkOrderSource.PreTripInspection,
            sourceRef: "TR-4818",
            createdBy: "Dispatch",
            assignedTo: null,
            dueDate: null,
            lineItems: lineItems ?? ["Brakes — Major"],
            shopId: null,
            authorizedLimitCad: null,
            budgetCode: null,
            dateRequiredOrOos: null).Value;

    private static InspectionDefect Defect(string item) =>
        new() { Item = item, Severity = InspectionDefectSeverity.Major, Note = "as found" };

    [Fact]
    public async Task Completing_stamps_the_source_inspections_defects_as_repaired_under_the_work_order()
    {
        var (handler, workOrders, services, inspections) = Setup();
        var vehicleId = Guid.NewGuid();
        // Built from every defect, as the Dispatcher's "Create work order" (all defects) does.
        var workOrder = OpenWorkOrder(vehicleId, ["Brakes — Major: as found", "Defroster — Major: as found"]);
        workOrders.Add(workOrder);

        var inspection = TestInspections.PreTrip(
            vehicleId: vehicleId, defects: [Defect("Brakes"), Defect("Defroster")]);
        Assert.True(inspection.LinkWorkOrder(workOrder.Id).IsSuccess);
        inspections.Add(inspection);

        var result = await handler.Handle(Command(workOrder.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Single(services.Records);

        Assert.All(inspection.Defects, defect =>
        {
            Assert.True(defect.IsResolved);
            Assert.Equal(DefectResolutionReason.RepairedUnderWorkOrder, defect.ResolutionReason);
            Assert.Equal(workOrder.Id, defect.ResolvedByWorkOrderId);
            Assert.Equal("M. Cardinal", defect.ResolvedBy);
        });

        // One save — the work order, the service record, and the inspection share a DbContext.
        Assert.Equal(1, workOrders.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_defect_already_resolved_by_hand_keeps_its_own_attribution()
    {
        var (handler, workOrders, _, inspections) = Setup();
        var vehicleId = Guid.NewGuid();
        var workOrder = OpenWorkOrder(vehicleId);
        workOrders.Add(workOrder);

        var inspection = TestInspections.PreTrip(
            vehicleId: vehicleId, defects: [Defect("Brakes"), Defect("Defroster")]);
        Assert.True(inspection.LinkWorkOrder(workOrder.Id).IsSuccess);
        Assert.True(inspection
            .ResolveDefect("Defroster", DefectResolutionReason.ReportedInError, "not actually faulty", "Dispatch", DateTimeOffset.UtcNow)
            .IsSuccess);
        inspections.Add(inspection);

        var result = await handler.Handle(Command(workOrder.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var defroster = inspection.Defects.Single(d => d.Item == "Defroster");
        Assert.Equal(DefectResolutionReason.ReportedInError, defroster.ResolutionReason);
        Assert.Equal("Dispatch", defroster.ResolvedBy);
        Assert.Null(defroster.ResolvedByWorkOrderId);

        var brakes = inspection.Defects.Single(d => d.Item == "Brakes");
        Assert.Equal(DefectResolutionReason.RepairedUnderWorkOrder, brakes.ResolutionReason);
    }

    /// <summary>Links <paramref name="workOrder"/> to a fresh 3-defect inspection and completes it.</summary>
    private static async Task<VehicleInspection> CompleteAgainstThreeDefectInspection(WorkOrder workOrder)
    {
        var (handler, workOrders, _, inspections) = Setup();
        workOrders.Add(workOrder);

        var inspection = TestInspections.PreTrip(
            vehicleId: workOrder.VehicleId,
            defects: [Defect("Brakes"), Defect("Defroster"), Defect("Wipers")]);
        Assert.True(inspection.LinkWorkOrder(workOrder.Id).IsSuccess);
        inspections.Add(inspection);

        var result = await handler.Handle(Command(workOrder.Id), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return inspection;
    }

    [Fact]
    public async Task A_work_order_built_for_one_defect_resolves_only_that_defect()
    {
        // Regression: the per-item "Create work order" builds a single line item, yet completing
        // it used to stamp every defect on the inspection as repaired.
        var workOrder = OpenWorkOrder(Guid.NewGuid(), ["Defroster — Major: as found"]);

        var inspection = await CompleteAgainstThreeDefectInspection(workOrder);

        var defroster = inspection.Defects.Single(d => d.Item == "Defroster");
        Assert.Equal(DefectResolutionReason.RepairedUnderWorkOrder, defroster.ResolutionReason);
        Assert.Equal(workOrder.Id, defroster.ResolvedByWorkOrderId);

        Assert.False(inspection.Defects.Single(d => d.Item == "Brakes").IsResolved);
        Assert.False(inspection.Defects.Single(d => d.Item == "Wipers").IsResolved);

        // The siblings are still open in the ordinary sense — a dispatcher can clear them by hand.
        Assert.True(inspection
            .ResolveDefect("Wipers", DefectResolutionReason.PreviouslyRepaired, null, "Dispatch", DateTimeOffset.UtcNow)
            .IsSuccess);
        Assert.False(inspection.Defects.Single(d => d.Item == "Brakes").IsResolved);
    }

    [Fact]
    public async Task A_work_order_built_from_every_defect_resolves_them_all()
    {
        var workOrder = OpenWorkOrder(
            Guid.NewGuid(),
            ["Brakes — Major: as found", "Defroster — Major: as found", "Wipers — Major: as found"]);

        var inspection = await CompleteAgainstThreeDefectInspection(workOrder);

        Assert.All(inspection.Defects, d =>
        {
            Assert.Equal(DefectResolutionReason.RepairedUnderWorkOrder, d.ResolutionReason);
            Assert.Equal(workOrder.Id, d.ResolvedByWorkOrderId);
        });
    }

    [Fact]
    public async Task A_work_order_whose_line_items_name_no_defect_resolves_them_all_as_before()
    {
        // A manually written work order (or one whose line items were rewritten) gives no
        // per-defect signal — keep the legacy whole-inspection behaviour rather than resolving
        // nothing.
        var workOrder = OpenWorkOrder(Guid.NewGuid(), ["Replace brake line", "Inspect cab heater"]);

        var inspection = await CompleteAgainstThreeDefectInspection(workOrder);

        Assert.All(inspection.Defects, d =>
            Assert.Equal(DefectResolutionReason.RepairedUnderWorkOrder, d.ResolutionReason));
    }

    [Fact]
    public async Task A_one_defect_work_order_whose_defect_was_cleared_by_hand_resolves_nothing_else()
    {
        // Coverage is decided against every defect, resolved or not — otherwise a cleared
        // target would fall back to "resolve all" and sweep its siblings.
        var (handler, workOrders, _, inspections) = Setup();
        var vehicleId = Guid.NewGuid();
        var workOrder = OpenWorkOrder(vehicleId, ["Brakes — Major: as found"]);
        workOrders.Add(workOrder);

        var inspection = TestInspections.PreTrip(
            vehicleId: vehicleId, defects: [Defect("Brakes"), Defect("Defroster")]);
        Assert.True(inspection.LinkWorkOrder(workOrder.Id).IsSuccess);
        Assert.True(inspection
            .ResolveDefect("Brakes", DefectResolutionReason.PreviouslyRepaired, null, "Dispatch", DateTimeOffset.UtcNow)
            .IsSuccess);
        inspections.Add(inspection);

        var result = await handler.Handle(Command(workOrder.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Dispatch", inspection.Defects.Single(d => d.Item == "Brakes").ResolvedBy);
        Assert.False(inspection.Defects.Single(d => d.Item == "Defroster").IsResolved);
    }

    [Fact]
    public async Task A_work_order_with_no_source_inspection_completes_cleanly()
    {
        // A work order can be raised directly, with no DVIR behind it. Nothing to resolve is the
        // normal case there, not an error.
        var (handler, workOrders, services, inspections) = Setup();
        var workOrder = OpenWorkOrder(Guid.NewGuid());
        workOrders.Add(workOrder);

        var result = await handler.Handle(Command(workOrder.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Equal(result.Value, Assert.Single(services.Records).Id);
        Assert.Empty(inspections.Inspections);
        Assert.Equal(1, workOrders.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_unknown_work_order_fails_with_not_found_and_saves_nothing()
    {
        var (handler, workOrders, services, _) = Setup();

        var result = await handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkOrderErrors.NotFound, result.Error);
        Assert.Empty(services.Records);
        Assert.Equal(0, workOrders.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_terminal_work_order_cannot_be_completed_again_and_resolves_nothing()
    {
        var (handler, workOrders, _, inspections) = Setup();
        var vehicleId = Guid.NewGuid();
        var workOrder = OpenWorkOrder(vehicleId);
        Assert.True(workOrder.ChangeStatus(WorkOrderStatus.Cancelled).IsSuccess);
        workOrders.Add(workOrder);

        var inspection = TestInspections.PreTrip(vehicleId: vehicleId, defects: [Defect("Brakes")]);
        Assert.True(inspection.LinkWorkOrder(workOrder.Id).IsSuccess);
        inspections.Add(inspection);

        var result = await handler.Handle(Command(workOrder.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkOrderErrors.Terminal, result.Error);

        // A cancelled work order resolves nothing: the defect stays open, still tagged with the
        // work order so a dispatcher sees a repair was attempted and called off.
        Assert.False(Assert.Single(inspection.Defects).IsResolved);
        Assert.Equal(0, workOrders.SaveChangesCallCount);
    }
}
