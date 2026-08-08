using NorthernLink.Fleet.Application.WorkOrders.Create;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.WorkOrders;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class CreateWorkOrderCommandHandlerTests
{
    private static CreateWorkOrderCommand Command(Guid vehicleId, Guid? inspectionId = null) =>
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
            InspectionId: inspectionId);

    private static (CreateWorkOrderCommandHandler Handler,
        InMemoryWorkOrderRepository WorkOrders,
        InMemoryVehicleInspectionRepository Inspections,
        Guid VehicleId) Setup()
    {
        var workOrders = new InMemoryWorkOrderRepository();
        var inspections = new InMemoryVehicleInspectionRepository();
        var vehicleId = Guid.NewGuid();
        workOrders.KnownVehicleIds.Add(vehicleId);

        return (new CreateWorkOrderCommandHandler(workOrders, inspections), workOrders, inspections, vehicleId);
    }

    [Fact]
    public async Task Creates_a_work_order_with_the_next_tenant_number()
    {
        var (handler, workOrders, _, vehicleId) = Setup();

        var result = await handler.Handle(Command(vehicleId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(workOrders.WorkOrders);
        Assert.Equal(result.Value, stored.Id);
        Assert.Equal("WO-1", stored.Number);
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
    public async Task An_inspection_id_links_the_inspection_to_the_new_work_order()
    {
        var (handler, workOrders, inspections, vehicleId) = Setup();
        var inspection = TestInspections.PostTrip(
            vehicleId: vehicleId,
            defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);
        inspections.Add(inspection);

        var result = await handler.Handle(
            Command(vehicleId, inspection.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(workOrders.WorkOrders);
        Assert.Equal(stored.Id, inspection.GeneratedWorkOrderId);
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
    public async Task An_already_linked_inspection_fails_with_conflict_and_saves_nothing()
    {
        var (handler, workOrders, inspections, vehicleId) = Setup();
        var inspection = TestInspections.PostTrip(
            vehicleId: vehicleId,
            defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);
        Assert.True(inspection.LinkWorkOrder(Guid.NewGuid()).IsSuccess);
        inspections.Add(inspection);

        var result = await handler.Handle(
            Command(vehicleId, inspection.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.WorkOrderAlreadyGenerated, result.Error);
        Assert.Equal(0, workOrders.SaveChangesCallCount);
    }

    [Fact]
    public void A_second_link_returns_conflict_and_raises_no_second_event()
    {
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
