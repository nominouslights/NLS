using NorthernLink.Fleet.Application.Inspections.Enter;
using NorthernLink.Fleet.Application.Inspections.Update;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.WorkOrders;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The amend handler: loads tenant-filtered (a missing/cross-tenant id yields
/// <see cref="InspectionErrors.NotFound"/>), then delegates to <see cref="VehicleInspection.Amend"/>
/// and saves.
/// </summary>
public class UpdateInspectionCommandHandlerTests
{
    private static UpdateInspectionCommand Command(Guid inspectionId, IReadOnlyList<DefectInput> defects, string unit = "U-04") =>
        new(
            inspectionId,
            InspectionSource.Dispatcher,
            VehicleId: null,
            Unit: unit,
            DriverName: "J. Spence",
            EnteredBy: null,
            PerformedAt: DateTimeOffset.UtcNow,
            OdometerKm: 118_500,
            Checklist: [],
            Defects: defects,
            Weather: [],
            TemperatureC: null,
            RoadConditions: [],
            Visibility: null,
            RoadAdvisories: null,
            FuelLevel: null,
            Issues: [],
            Attestations: [],
            DriverSignatureName: null,
            CertifiedAt: null,
            FuelAdded: false,
            FuelLitres: null,
            FuelCostCad: null);

    [Fact]
    public async Task An_unknown_id_returns_not_found()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var handler = new UpdateInspectionCommandHandler(repository, new InMemoryWorkOrderRepository());

        var result = await handler.Handle(Command(Guid.NewGuid(), []), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.NotFound, result.Error);
        Assert.Equal(0, repository.SaveChangesCallCount); // nothing to save
    }

    [Fact]
    public async Task Amends_the_stored_inspection_re_derives_the_result_and_saves()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var stored = TestInspections.PreTrip(defects: []); // Pass to begin with
        repository.Add(stored);
        Assert.Equal(InspectionResult.Pass, stored.Result);

        var handler = new UpdateInspectionCommandHandler(repository, new InMemoryWorkOrderRepository());
        var result = await handler.Handle(
            Command(stored.Id, [new DefectInput("Brakes", InspectionDefectSeverity.OutOfService, null)]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InspectionResult.Fail, stored.Result);
        // Identity is untouched by the amend.
        Assert.Equal(InspectionType.PreTrip, stored.Type);
        Assert.Equal("TR-4818", stored.TripNumber);
        Assert.Equal(118_500, stored.OdometerKm);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }
    /// <summary>
    /// A unit-only inspection ("Brakes" open, "Wipers" resolved by hand) whose legacy
    /// whole-inspection work order is in the work-order repository in <paramref name="status"/> —
    /// or, for null, an id naming no work order at all.
    /// </summary>
    private static (UpdateInspectionCommandHandler Handler, InMemoryVehicleInspectionRepository Inspections, VehicleInspection Inspection)
        WithLegacyWorkOrder(WorkOrderStatus? status)
    {
        var inspections = new InMemoryVehicleInspectionRepository();
        var workOrders = new InMemoryWorkOrderRepository();
        var inspection = TestInspections.PreTrip(defects: [
            new InspectionDefect { Item = "Brakes", Severity = InspectionDefectSeverity.Major },
            new InspectionDefect { Item = "Wipers", Severity = InspectionDefectSeverity.Minor },
        ]);
        Assert.True(inspection
            .ResolveDefect("Wipers", DefectResolutionReason.PreviouslyRepaired, null, "Dispatch", DateTimeOffset.UtcNow)
            .IsSuccess);

        var legacy = WorkOrder.Create(
            TestVehicles.TenantId, Guid.NewGuid(), "WO-LEGACY", "Repair DVIR defects", null,
            WorkOrderPriority.High, WorkOrderSource.PreTripInspection, null, "Dispatch", null, null,
            ["Brakes — Major", "Wipers — Minor"], null, null, null, null).Value;
        Assert.True(inspection.LinkWorkOrder(legacy.Id).IsSuccess);

        switch (status)
        {
            case null:
                break; // the id names nothing in the repository
            case WorkOrderStatus.Completed:
                Assert.True(legacy.Complete(Guid.NewGuid()).IsSuccess);
                workOrders.Add(legacy);
                break;
            case WorkOrderStatus.Open:
                workOrders.Add(legacy);
                break;
            default:
                Assert.True(legacy.ChangeStatus(status.Value).IsSuccess);
                workOrders.Add(legacy);
                break;
        }

        inspections.Add(inspection);
        return (new UpdateInspectionCommandHandler(inspections, workOrders), inspections, inspection);
    }

    private static readonly DefectInput Brakes = new("Brakes", InspectionDefectSeverity.Major, null);
    private static readonly DefectInput Wipers = new("Wipers", InspectionDefectSeverity.Minor, null);

    [Theory]
    [InlineData(WorkOrderStatus.Open)]
    [InlineData(WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.AwaitingParts)]
    public async Task An_open_legacy_work_order_blocks_dropping_its_unresolved_defect(WorkOrderStatus status)
    {
        var (handler, inspections, inspection) = WithLegacyWorkOrder(status);

        var result = await handler.Handle(Command(inspection.Id, [Wipers]), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.AttachedDefectCannotBeDropped("Brakes"), result.Error);
        Assert.Equal("Fleet.Inspection.DefectOnActiveWorkOrder", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.NotNull(inspection.FindDefect("Brakes"));
        Assert.Equal(0, inspections.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_open_legacy_work_order_blocks_a_vehicle_change()
    {
        var (handler, inspections, inspection) = WithLegacyWorkOrder(WorkOrderStatus.Open);

        var result = await handler.Handle(Command(inspection.Id, [Brakes, Wipers], unit: "U-07"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.VehicleChangeWithDefectOnActiveWorkOrder, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("U-04", inspection.Unit);
        Assert.Equal(0, inspections.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_resolved_defect_under_an_open_legacy_work_order_can_still_be_dropped()
    {
        // Resolved means no longer held: the legacy completion skips resolved defects, so
        // nothing it will do depends on the row. Only the unresolved "Brakes" stays locked.
        var (handler, inspections, inspection) = WithLegacyWorkOrder(WorkOrderStatus.Open);

        var result = await handler.Handle(Command(inspection.Id, [Brakes]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(inspection.FindDefect("Wipers"));
        Assert.Equal(1, inspections.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.Cancelled)]
    [InlineData(null)]
    public async Task A_closed_or_missing_legacy_work_order_holds_nothing(WorkOrderStatus? status)
    {
        var (handler, _, dropped) = WithLegacyWorkOrder(status);
        var dropResult = await handler.Handle(Command(dropped.Id, [Wipers]), CancellationToken.None);
        Assert.True(dropResult.IsSuccess);
        Assert.Null(dropped.FindDefect("Brakes"));

        var (handler2, _, moved) = WithLegacyWorkOrder(status);
        var moveResult = await handler2.Handle(Command(moved.Id, [Brakes, Wipers], unit: "U-07"), CancellationToken.None);
        Assert.True(moveResult.IsSuccess);
        Assert.Equal("U-07", moved.Unit);
    }
}