using NorthernLink.Fleet.Application.Inspections.Remove;
using NorthernLink.Fleet.Application.WorkOrders.ChangeStatus;
using NorthernLink.Fleet.Application.WorkOrders.Complete;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Services;
using NorthernLink.Fleet.Domain.WorkOrders;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The handler half of the per-defect link: completing applies each line's outcome to exactly
/// its own defect, cancelling releases the defects, and an inspection under an open work order
/// cannot be removed. (Work orders WITHOUT lines keep the #106 behaviour — see
/// <see cref="CompleteWorkOrderCommandHandlerTests"/>, unchanged.)
/// </summary>
public class WorkOrderDefectOutcomeHandlerTests
{
    private sealed class Fixture
    {
        public InMemoryWorkOrderRepository WorkOrders { get; } = new();
        public InMemoryServiceRecordRepository Services { get; } = new();
        public InMemoryVehicleInspectionRepository Inspections { get; } = new();
        public Guid VehicleId { get; } = Guid.NewGuid();

        public CompleteWorkOrderCommandHandler Complete => new(WorkOrders, Services, Inspections);

        public ChangeWorkOrderStatusCommandHandler ChangeStatus => new(WorkOrders, Inspections);

        public VehicleInspection Inspection(params InspectionDefect[] defects)
        {
            var inspection = TestInspections.PreTrip(vehicleId: VehicleId, defects: defects);
            Inspections.Add(inspection);
            return inspection;
        }

        /// <summary>A work order raised against the named defects, attached exactly as the create handler does.</summary>
        public WorkOrder WorkOrderFor(params (VehicleInspection Inspection, string Item)[] defects)
        {
            var workOrder = WorkOrder.Create(
                TestVehicles.TenantId,
                VehicleId,
                "WO-T1",
                "Repair DVIR defects",
                description: null,
                WorkOrderPriority.High,
                WorkOrderSource.PreTripInspection,
                sourceRef: null,
                createdBy: "Dispatch",
                assignedTo: null,
                dueDate: null,
                // Deliberately names EVERY defect: line items must no longer decide anything for
                // a work order that has real defect lines.
                lineItems: ["Brakes — Major", "Wipers — Major", "Horn — Major", "Mirrors — Major"],
                shopId: null,
                authorizedLimitCad: null,
                budgetCode: null,
                dateRequiredOrOos: null,
                defects.Select(d =>
                {
                    var defect = d.Inspection.FindDefect(d.Item)!;
                    return new WorkOrderDefectLine
                    {
                        InspectionId = d.Inspection.Id,
                        Item = defect.Item,
                        Severity = defect.Severity,
                        Note = defect.Note,
                    };
                }).ToList()).Value;

            foreach (var (inspection, item) in defects)
            {
                Assert.True(inspection.AssignDefectToWorkOrder(item, workOrder.Id).IsSuccess);
            }

            WorkOrders.Add(workOrder);
            return workOrder;
        }
    }

    private static InspectionDefect Defect(string item, InspectionDefectSeverity severity = InspectionDefectSeverity.Major) =>
        new() { Item = item, Severity = severity, Note = "as found" };

    private static CompleteWorkOrderCommand Command(
        Guid workOrderId,
        IReadOnlyList<WorkOrderDefectOutcome>? outcomes,
        string performedBy = " M. Cardinal ") =>
        new(
            TestVehicles.TenantId,
            workOrderId,
            Date: DateTimeOffset.UtcNow,
            PerformedBy: performedBy,
            Category: ServiceCategory.InspectionFix,
            OdometerKm: 118_500,
            ItemsChanged: ["Brake pads"],
            Reason: "DVIR defect",
            PartsUsed: [],
            LaborHours: 2m,
            CostCad: 300m,
            Notes: null,
            DefectOutcomes: outcomes);

    private static WorkOrderDefectOutcome Outcome(
        VehicleInspection inspection, string item, DefectRepairOutcome outcome, string? note = null) =>
        new(inspection.Id, item, outcome, note);

    [Fact]
    public async Task Repaired_resolves_only_the_listed_defect_and_its_sibling_stays_open()
    {
        var f = new Fixture();
        var inspection = f.Inspection(Defect("Brakes"), Defect("Wipers"));
        var workOrder = f.WorkOrderFor((inspection, "Brakes"));

        var result = await f.Complete.Handle(
            Command(workOrder.Id, [Outcome(inspection, "Brakes", DefectRepairOutcome.Repaired, "new pads")]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);

        var brakes = inspection.FindDefect("Brakes")!;
        Assert.Equal(DefectResolutionReason.RepairedUnderWorkOrder, brakes.ResolutionReason);
        Assert.Equal(workOrder.Id, brakes.ResolvedByWorkOrderId);
        Assert.Equal("M. Cardinal", brakes.ResolvedBy); // the service record's PerformedBy
        Assert.Equal("new pads", brakes.ResolutionNote);
        Assert.Null(brakes.WorkOrderId);

        var wipers = inspection.FindDefect("Wipers")!;
        Assert.False(wipers.IsResolved);
        Assert.Null(wipers.WorkOrderId);

        Assert.Single(f.Services.Records);
        Assert.Equal(1, f.WorkOrders.SaveChangesCallCount);
    }

    [Fact]
    public async Task Each_line_gets_its_own_outcome_across_two_inspections()
    {
        var f = new Fixture();
        var pre = f.Inspection(Defect("Brakes"), Defect("Wipers", InspectionDefectSeverity.Minor));
        var post = f.Inspection(Defect("Horn"));
        var workOrder = f.WorkOrderFor((pre, "Brakes"), (pre, "Wipers"), (post, "Horn"));

        var result = await f.Complete.Handle(
            Command(workOrder.Id, [
                Outcome(pre, "Brakes", DefectRepairOutcome.Repaired),
                Outcome(pre, "Wipers", DefectRepairOutcome.Deferred, "blade on back-order"),
                Outcome(post, "Horn", DefectRepairOutcome.NoFaultFound, "works on bench"),
            ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DefectResolutionReason.RepairedUnderWorkOrder, pre.FindDefect("Brakes")!.ResolutionReason);

        // Deferred: still open, released for a later work order.
        var wipers = pre.FindDefect("Wipers")!;
        Assert.False(wipers.IsResolved);
        Assert.Null(wipers.WorkOrderId);

        var horn = post.FindDefect("Horn")!;
        Assert.Equal(DefectResolutionReason.NoFaultFound, horn.ResolutionReason);
        Assert.Equal(workOrder.Id, horn.ResolvedByWorkOrderId);
        Assert.Equal("works on bench", horn.ResolutionNote);

        Assert.Equal(
            [DefectRepairOutcome.Repaired, DefectRepairOutcome.Deferred, DefectRepairOutcome.NoFaultFound],
            workOrder.Defects.Select(l => l.Outcome!.Value));
    }

    [Fact]
    public async Task Deferring_an_out_of_service_defect_is_rejected_and_nothing_changes()
    {
        var f = new Fixture();
        var inspection = f.Inspection(Defect("Brakes", InspectionDefectSeverity.OutOfService));
        var workOrder = f.WorkOrderFor((inspection, "Brakes"));

        var result = await f.Complete.Handle(
            Command(workOrder.Id, [Outcome(inspection, "Brakes", DefectRepairOutcome.Deferred, "no parts")]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkOrderErrors.OutOfServiceCannotBeDeferred, result.Error);
        Assert.Equal(WorkOrderStatus.Open, workOrder.Status);
        Assert.Empty(f.Services.Records);
        Assert.Equal(0, f.WorkOrders.SaveChangesCallCount);
        Assert.Equal(workOrder.Id, inspection.FindDefect("Brakes")!.WorkOrderId);
        Assert.False(inspection.FindDefect("Brakes")!.IsResolved);
    }

    [Fact]
    public async Task A_missing_outcome_is_rejected_and_nothing_is_saved()
    {
        var f = new Fixture();
        var inspection = f.Inspection(Defect("Brakes"), Defect("Wipers"));
        var workOrder = f.WorkOrderFor((inspection, "Brakes"), (inspection, "Wipers"));

        var result = await f.Complete.Handle(
            Command(workOrder.Id, [Outcome(inspection, "Brakes", DefectRepairOutcome.Repaired)]),
            CancellationToken.None);

        Assert.Equal(WorkOrderErrors.DefectOutcomeMissing, result.Error);
        Assert.Empty(f.Services.Records);
        Assert.Equal(0, f.WorkOrders.SaveChangesCallCount);
        Assert.All(inspection.Defects, d => Assert.False(d.IsResolved));
    }

    [Fact]
    public async Task A_defect_resolved_by_hand_meanwhile_keeps_its_own_stamp()
    {
        var f = new Fixture();
        var inspection = f.Inspection(Defect("Brakes"));
        var workOrder = f.WorkOrderFor((inspection, "Brakes"));
        Assert.True(inspection
            .ResolveDefect("Brakes", DefectResolutionReason.PreviouslyRepaired, "fixed on the road", "Dispatch", DateTimeOffset.UtcNow)
            .IsSuccess);

        var result = await f.Complete.Handle(
            Command(workOrder.Id, [Outcome(inspection, "Brakes", DefectRepairOutcome.Repaired)]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var brakes = inspection.FindDefect("Brakes")!;
        Assert.Equal(DefectResolutionReason.PreviouslyRepaired, brakes.ResolutionReason);
        Assert.Equal("Dispatch", brakes.ResolvedBy);
        Assert.Null(brakes.ResolvedByWorkOrderId);
        Assert.Null(brakes.WorkOrderId);
        Assert.False(inspection.HasDefectOnActiveWorkOrder);
    }

    [Fact]
    public async Task A_removed_inspection_or_an_amended_away_item_is_skipped_but_the_outcome_is_recorded()
    {
        var f = new Fixture();
        var gone = f.Inspection(Defect("Horn"));
        var amended = f.Inspection(Defect("Brakes"), Defect("Wipers"));
        var workOrder = f.WorkOrderFor((gone, "Horn"), (amended, "Brakes"));

        f.Inspections.Remove(gone);
        Assert.True(TestInspections.AmendWith(amended, [Defect("Wipers")]).IsSuccess);

        var result = await f.Complete.Handle(
            Command(workOrder.Id, [
                Outcome(gone, "Horn", DefectRepairOutcome.Repaired),
                Outcome(amended, "Brakes", DefectRepairOutcome.Repaired),
            ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(workOrder.Defects, l => Assert.Equal(DefectRepairOutcome.Repaired, l.Outcome));
        Assert.False(amended.FindDefect("Wipers")!.IsResolved);
    }

    [Fact]
    public async Task Cancelling_releases_the_work_orders_defects_and_leaves_them_open()
    {
        var f = new Fixture();
        var pre = f.Inspection(Defect("Brakes"), Defect("Wipers"));
        var post = f.Inspection(Defect("Horn"));
        var workOrder = f.WorkOrderFor((pre, "Brakes"), (post, "Horn"));
        var other = Guid.NewGuid();
        Assert.True(pre.AssignDefectToWorkOrder("Wipers", other).IsSuccess);

        var result = await f.ChangeStatus.Handle(
            new ChangeWorkOrderStatusCommand(TestVehicles.TenantId, workOrder.Id, WorkOrderStatus.Cancelled),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderStatus.Cancelled, workOrder.Status);
        Assert.Null(pre.FindDefect("Brakes")!.WorkOrderId);
        Assert.Null(post.FindDefect("Horn")!.WorkOrderId);
        Assert.False(pre.FindDefect("Brakes")!.IsResolved);
        // Another work order's defect is untouched.
        Assert.Equal(other, pre.FindDefect("Wipers")!.WorkOrderId);
        // The cancelled work order still records what it was raised for.
        Assert.Equal(2, workOrder.Defects.Count);
        Assert.Equal(1, f.WorkOrders.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_non_cancelling_status_change_keeps_the_defects_attached()
    {
        var f = new Fixture();
        var inspection = f.Inspection(Defect("Brakes"));
        var workOrder = f.WorkOrderFor((inspection, "Brakes"));

        var result = await f.ChangeStatus.Handle(
            new ChangeWorkOrderStatusCommand(TestVehicles.TenantId, workOrder.Id, WorkOrderStatus.InProgress),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(workOrder.Id, inspection.FindDefect("Brakes")!.WorkOrderId);
    }

    [Fact]
    public async Task An_inspection_with_a_defect_on_an_open_work_order_cannot_be_removed()
    {
        var f = new Fixture();
        var inspection = f.Inspection(Defect("Brakes"));
        var workOrder = f.WorkOrderFor((inspection, "Brakes"));
        var remove = new RemoveInspectionCommandHandler(f.Inspections);

        var blocked = await remove.Handle(new RemoveInspectionCommand(inspection.Id), CancellationToken.None);

        Assert.Equal(InspectionErrors.DefectOnActiveWorkOrder, blocked.Error);
        Assert.Single(f.Inspections.Inspections);
        Assert.Equal(0, f.Inspections.SaveChangesCallCount);

        // Once the work order is cancelled the defect is released and removal goes through.
        Assert.True((await f.ChangeStatus.Handle(
            new ChangeWorkOrderStatusCommand(TestVehicles.TenantId, workOrder.Id, WorkOrderStatus.Cancelled),
            CancellationToken.None)).IsSuccess);

        var removed = await remove.Handle(new RemoveInspectionCommand(inspection.Id), CancellationToken.None);
        Assert.True(removed.IsSuccess);
        Assert.Empty(f.Inspections.Inspections);
    }
}
