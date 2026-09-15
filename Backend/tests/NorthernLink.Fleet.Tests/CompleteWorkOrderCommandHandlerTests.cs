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

    private static WorkOrder OpenWorkOrder(Guid vehicleId) =>
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
            lineItems: ["Brakes — Major"],
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
        var workOrder = OpenWorkOrder(vehicleId);
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
