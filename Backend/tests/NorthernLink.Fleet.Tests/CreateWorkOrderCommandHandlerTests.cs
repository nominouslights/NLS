using NorthernLink.Fleet.Application.WorkOrders.Create;
using NorthernLink.Shared.Kernel;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.WorkOrders;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// Creating a work order attaches each named defect to it — per defect, not per inspection — and
/// every check runs before anything changes, so a failure saves nothing and attaches nothing.
/// </summary>
public class CreateWorkOrderCommandHandlerTests
{
    private static CreateWorkOrderCommand Command(
        Guid vehicleId,
        Guid? inspectionId = null,
        IReadOnlyList<WorkOrderDefectRef>? defects = null) =>
        new(
            TestVehicles.TenantId,
            vehicleId,
            Title: "Replace wiper assembly",
            Description: "Driver-side wiper skipping",
            Priority: WorkOrderPriority.High,
            Source: WorkOrderSource.PostTripInspection,
            SourceRef: "TR-4818",
            AssignedTo: null,
            DueDate: null,
            LineItems: ["Wipers & washer fluid — Major"],
            ShopId: null,
            AuthorizedLimitCad: null,
            BudgetCode: null,
            DateRequiredOrOos: null,
            InspectionId: inspectionId,
            Defects: defects);

    private static (CreateWorkOrderCommandHandler Handler,
        InMemoryWorkOrderRepository WorkOrders,
        InMemoryVehicleInspectionRepository Inspections,
        Guid VehicleId) Setup()
    {
        var workOrders = new InMemoryWorkOrderRepository();
        var inspections = new InMemoryVehicleInspectionRepository();
        var vehicleId = Guid.NewGuid();
        workOrders.KnownVehicleIds.Add(vehicleId);
        workOrders.VehicleUnitNumbers[vehicleId] = "U-04";

        return (new CreateWorkOrderCommandHandler(workOrders, inspections), workOrders, inspections, vehicleId);
    }

    private static InspectionDefect Defect(
        string item,
        InspectionDefectSeverity severity = InspectionDefectSeverity.Major,
        string? note = "as found") =>
        new() { Item = item, Severity = severity, Note = note };

    [Fact]
    public async Task Creates_a_work_order_with_the_next_tenant_number()
    {
        var (handler, workOrders, _, vehicleId) = Setup();

        var result = await handler.Handle(Command(vehicleId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(workOrders.WorkOrders);
        Assert.Equal(result.Value, stored.Id);
        Assert.Equal("WO-1", stored.Number);
        Assert.Empty(stored.Defects);
        Assert.Equal(1, workOrders.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_unknown_vehicle_fails_with_not_found()
    {
        var (handler, workOrders, _, _) = Setup();

        var result = await handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.NotFound, result.Error);
        Assert.Empty(workOrders.WorkOrders);
        Assert.Equal(0, workOrders.SaveChangesCallCount);
    }

    [Fact]
    public async Task One_named_defect_is_attached_and_copied_onto_a_line()
    {
        var (handler, workOrders, inspections, vehicleId) = Setup();
        var inspection = TestInspections.PostTrip(
            vehicleId: vehicleId,
            defects: [Defect("Brakes", InspectionDefectSeverity.OutOfService, "grinding"), Defect("Defroster")]);
        inspections.Add(inspection);

        var result = await handler.Handle(
            Command(vehicleId, defects: [new(inspection.Id, " brakes ")]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var workOrder = Assert.Single(workOrders.WorkOrders);

        var line = Assert.Single(workOrder.Defects);
        Assert.Equal(inspection.Id, line.InspectionId);
        Assert.Equal("Brakes", line.Item); // the inspection's spelling, not the request's
        Assert.Equal(InspectionDefectSeverity.OutOfService, line.Severity);
        Assert.Equal("grinding", line.Note);
        Assert.Null(line.Outcome);

        Assert.Equal(workOrder.Id, inspection.FindDefect("Brakes")!.WorkOrderId);
        Assert.Null(inspection.FindDefect("Defroster")!.WorkOrderId);

        // The whole-inspection link is history now — new work orders never set it.
        Assert.Null(inspection.GeneratedWorkOrderId);
        Assert.Equal(1, workOrders.SaveChangesCallCount);
    }

    [Fact]
    public async Task Several_defects_across_two_inspections_of_the_same_vehicle_go_on_one_work_order()
    {
        var (handler, workOrders, inspections, vehicleId) = Setup();
        var pre = TestInspections.PreTrip(vehicleId: vehicleId, defects: [Defect("Brakes"), Defect("Wipers")]);
        var post = TestInspections.PostTrip(vehicleId: vehicleId, defects: [Defect("Headlights")]);
        inspections.Add(pre);
        inspections.Add(post);

        var result = await handler.Handle(
            Command(vehicleId, defects: [new(pre.Id, "Brakes"), new(post.Id, "Headlights")]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var workOrder = Assert.Single(workOrders.WorkOrders);
        Assert.Equal(2, workOrder.Defects.Count);
        Assert.Equal(workOrder.Id, pre.FindDefect("Brakes")!.WorkOrderId);
        Assert.Equal(workOrder.Id, post.FindDefect("Headlights")!.WorkOrderId);
        Assert.Null(pre.FindDefect("Wipers")!.WorkOrderId);
        Assert.Equal(1, workOrders.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_defect_from_another_vehicles_inspection_is_rejected_and_nothing_is_saved()
    {
        var (handler, workOrders, inspections, vehicleId) = Setup();
        var own = TestInspections.PreTrip(vehicleId: vehicleId, defects: [Defect("Brakes")]);
        var other = TestInspections.PostTrip(vehicleId: Guid.NewGuid(), defects: [Defect("Wipers")]);
        inspections.Add(own);
        inspections.Add(other);

        var result = await handler.Handle(
            Command(vehicleId, defects: [new(own.Id, "Brakes"), new(other.Id, "Wipers")]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.VehicleMismatch, result.Error);
        Assert.Empty(workOrders.WorkOrders);
        Assert.Equal(0, workOrders.SaveChangesCallCount);
        Assert.Null(own.FindDefect("Brakes")!.WorkOrderId);
        Assert.Null(other.FindDefect("Wipers")!.WorkOrderId);
    }

    [Fact]
    public async Task A_legacy_unit_only_inspection_matches_on_the_vehicles_unit_number()
    {
        var (handler, workOrders, inspections, vehicleId) = Setup();
        var sameUnit = TestInspections.PreTrip(vehicleId: null, unit: "U-04", defects: [Defect("Brakes")]);
        var otherUnit = TestInspections.PostTrip(vehicleId: null, unit: "U-07", defects: [Defect("Wipers")]);
        inspections.Add(sameUnit);
        inspections.Add(otherUnit);

        var ok = await handler.Handle(Command(vehicleId, defects: [new(sameUnit.Id, "Brakes")]), CancellationToken.None);
        Assert.True(ok.IsSuccess);

        var mismatch = await handler.Handle(Command(vehicleId, defects: [new(otherUnit.Id, "Wipers")]), CancellationToken.None);
        Assert.True(mismatch.IsFailure);
        Assert.Equal(InspectionErrors.VehicleMismatch, mismatch.Error);
        Assert.Single(workOrders.WorkOrders);
    }

    [Fact]
    public async Task A_defect_already_on_an_active_work_order_is_a_conflict_and_nothing_is_saved()
    {
        var (handler, workOrders, inspections, vehicleId) = Setup();
        var inspection = TestInspections.PreTrip(vehicleId: vehicleId, defects: [Defect("Brakes"), Defect("Wipers")]);
        var earlierWorkOrderId = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Brakes", earlierWorkOrderId).IsSuccess);
        inspections.Add(inspection);

        var result = await handler.Handle(
            Command(vehicleId, defects: [new(inspection.Id, "Wipers"), new(inspection.Id, "Brakes")]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.DefectAlreadyOnWorkOrder, result.Error);
        Assert.Empty(workOrders.WorkOrders);
        Assert.Equal(0, workOrders.SaveChangesCallCount);
        Assert.Equal(earlierWorkOrderId, inspection.FindDefect("Brakes")!.WorkOrderId);
        // Validation runs before any attach — the defect listed first was not touched.
        Assert.Null(inspection.FindDefect("Wipers")!.WorkOrderId);
    }

    [Fact]
    public async Task A_resolved_or_unknown_defect_is_rejected()
    {
        var (handler, workOrders, inspections, vehicleId) = Setup();
        var inspection = TestInspections.PreTrip(vehicleId: vehicleId, defects: [Defect("Brakes")]);
        Assert.True(inspection
            .ResolveDefect("Brakes", DefectResolutionReason.PreviouslyRepaired, null, "Dispatch", DateTimeOffset.UtcNow)
            .IsSuccess);
        inspections.Add(inspection);

        var resolved = await handler.Handle(Command(vehicleId, defects: [new(inspection.Id, "Brakes")]), CancellationToken.None);
        Assert.Equal(InspectionErrors.DefectAlreadyResolved, resolved.Error);

        var unknown = await handler.Handle(Command(vehicleId, defects: [new(inspection.Id, "Mirrors")]), CancellationToken.None);
        Assert.Equal(InspectionErrors.DefectNotFound, unknown.Error);

        Assert.Empty(workOrders.WorkOrders);
        Assert.Equal(0, workOrders.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_same_defect_named_twice_is_rejected_and_nothing_is_attached()
    {
        var (handler, workOrders, inspections, vehicleId) = Setup();
        var inspection = TestInspections.PreTrip(vehicleId: vehicleId, defects: [Defect("Brakes")]);
        inspections.Add(inspection);

        var result = await handler.Handle(
            Command(vehicleId, defects: [new(inspection.Id, "Brakes"), new(inspection.Id, "BRAKES")]),
            CancellationToken.None);

        Assert.Equal(WorkOrderErrors.DuplicateDefect, result.Error);
        Assert.Empty(workOrders.WorkOrders);
        Assert.Null(inspection.FindDefect("Brakes")!.WorkOrderId);
    }

    [Fact]
    public async Task The_legacy_inspection_id_attaches_every_open_unattached_defect_of_that_inspection()
    {
        var (handler, workOrders, inspections, vehicleId) = Setup();
        var inspection = TestInspections.PostTrip(
            vehicleId: vehicleId,
            defects: [Defect("Brakes"), Defect("Wipers"), Defect("Mirrors"), Defect("Horn")]);
        Assert.True(inspection
            .ResolveDefect("Wipers", DefectResolutionReason.PreviouslyRepaired, null, "Dispatch", DateTimeOffset.UtcNow)
            .IsSuccess);
        var otherWorkOrderId = Guid.NewGuid();
        Assert.True(inspection.AssignDefectToWorkOrder("Mirrors", otherWorkOrderId).IsSuccess);
        inspections.Add(inspection);

        var result = await handler.Handle(Command(vehicleId, inspection.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var workOrder = Assert.Single(workOrders.WorkOrders);
        Assert.Equal(["Brakes", "Horn"], workOrder.Defects.Select(l => l.Item));
        Assert.Equal(workOrder.Id, inspection.FindDefect("Brakes")!.WorkOrderId);
        Assert.Equal(workOrder.Id, inspection.FindDefect("Horn")!.WorkOrderId);
        Assert.Equal(otherWorkOrderId, inspection.FindDefect("Mirrors")!.WorkOrderId);
        Assert.Null(inspection.FindDefect("Wipers")!.WorkOrderId);

        // It no longer goes through LinkWorkOrder — a second work order from the same inspection
        // is no longer blocked by a whole-inspection link.
        Assert.Null(inspection.GeneratedWorkOrderId);
        Assert.Empty(inspection.DomainEvents.OfType<VehicleInspectionWorkOrderLinkedDomainEvent>());
    }

    [Fact]
    public async Task The_legacy_inspection_id_and_an_explicit_defect_of_it_are_not_attached_twice()
    {
        var (handler, workOrders, inspections, vehicleId) = Setup();
        var inspection = TestInspections.PostTrip(vehicleId: vehicleId, defects: [Defect("Brakes"), Defect("Horn")]);
        inspections.Add(inspection);

        var result = await handler.Handle(
            Command(vehicleId, inspection.Id, defects: [new(inspection.Id, "Brakes")]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, Assert.Single(workOrders.WorkOrders).Defects.Count);
    }

    [Fact]
    public async Task The_legacy_inspection_id_with_every_defect_already_attached_is_a_conflict_and_nothing_is_saved()
    {
        // A double-submit: the first request attached everything; the second must not create an
        // empty, unlinked inspection-sourced work order whose completion resolves nothing.
        var (handler, workOrders, inspections, vehicleId) = Setup();
        var inspection = TestInspections.PostTrip(vehicleId: vehicleId, defects: [Defect("Brakes"), Defect("Horn")]);
        inspections.Add(inspection);

        var first = await handler.Handle(Command(vehicleId, inspection.Id), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await handler.Handle(Command(vehicleId, inspection.Id), CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal(InspectionErrors.NoOpenDefectsToAttach, second.Error);
        Assert.Equal(ErrorType.Conflict, second.Error.Type);
        Assert.Equal("Fleet.Inspection.NoOpenDefectsToAttach", second.Error.Code);
        Assert.Equal(first.Value, Assert.Single(workOrders.WorkOrders).Id);
        Assert.Equal(1, workOrders.SaveChangesCallCount);
        Assert.Equal(first.Value, inspection.FindDefect("Brakes")!.WorkOrderId);
        Assert.Equal(first.Value, inspection.FindDefect("Horn")!.WorkOrderId);
    }

    [Fact]
    public async Task The_legacy_inspection_id_with_every_defect_resolved_is_a_conflict_and_nothing_is_saved()
    {
        var (handler, workOrders, inspections, vehicleId) = Setup();
        var inspection = TestInspections.PostTrip(vehicleId: vehicleId, defects: [Defect("Brakes"), Defect("Horn")]);
        foreach (var item in new[] { "Brakes", "Horn" })
        {
            Assert.True(inspection
                .ResolveDefect(item, DefectResolutionReason.PreviouslyRepaired, null, "Dispatch", DateTimeOffset.UtcNow)
                .IsSuccess);
        }
        inspections.Add(inspection);

        var result = await handler.Handle(Command(vehicleId, inspection.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.NoOpenDefectsToAttach, result.Error);
        Assert.Empty(workOrders.WorkOrders);
        Assert.Equal(0, workOrders.SaveChangesCallCount);
        Assert.Null(inspection.FindDefect("Brakes")!.WorkOrderId);
        Assert.Null(inspection.FindDefect("Horn")!.WorkOrderId);
    }

    [Fact]
    public async Task A_manual_work_order_naming_neither_an_inspection_nor_defects_still_succeeds_with_no_lines()
    {
        var (handler, workOrders, _, vehicleId) = Setup();

        var result = await handler.Handle(Command(vehicleId, inspectionId: null, defects: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(Assert.Single(workOrders.WorkOrders).Defects);
        Assert.Equal(1, workOrders.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_unknown_inspection_id_fails_with_not_found_and_saves_nothing()
    {
        var (handler, workOrders, _, vehicleId) = Setup();

        var result = await handler.Handle(
            Command(vehicleId, inspectionId: Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.NotFound, result.Error);
        Assert.Empty(workOrders.WorkOrders);
        Assert.Equal(0, workOrders.SaveChangesCallCount);
    }

    [Fact]
    public void A_second_link_returns_conflict_and_raises_no_second_event()
    {
        // LinkWorkOrder survives for history (GeneratedWorkOrderId on pre-link work orders) and
        // keeps its rule, even though new work orders no longer call it.
        var inspection = TestInspections.PostTrip(
            defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);
        var firstWorkOrderId = Guid.NewGuid();

        Assert.True(inspection.LinkWorkOrder(firstWorkOrderId).IsSuccess);

        var second = inspection.LinkWorkOrder(Guid.NewGuid());

        Assert.True(second.IsFailure);
        Assert.Equal(InspectionErrors.WorkOrderAlreadyGenerated, second.Error);
        Assert.Equal(firstWorkOrderId, inspection.GeneratedWorkOrderId);

        var linkedEvent = Assert.Single(
            inspection.DomainEvents.OfType<VehicleInspectionWorkOrderLinkedDomainEvent>());
        Assert.Equal(firstWorkOrderId, linkedEvent.WorkOrderId);
    }
}
