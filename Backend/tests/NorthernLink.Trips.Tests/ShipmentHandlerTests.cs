using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Application.Shipments.AddLeg;
using NorthernLink.Trips.Application.Shipments.BulkAssign;
using NorthernLink.Trips.Application.Shipments.Register;
using NorthernLink.Trips.Application.Shipments.SetBilling;
using NorthernLink.Trips.Domain.Shipments;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Application-layer behaviour: number issuing, client validation against the existing
/// client_lookup replica, and — the one that matters most — that routing a shipment onto a trip
/// never touches the shipment's payer.
/// </summary>
public class ShipmentHandlerTests
{
    [Fact]
    public async Task Register_issues_a_server_side_number_and_snapshots_the_client_name()
    {
        var (shipments, clients, numbers) = Fakes();
        var handler = new RegisterShipmentCommandHandler(shipments, numbers, clients);

        var result = await handler.Handle(
            new RegisterShipmentCommand(
                TestShipments.TenantId,
                TestShipments.Details(clientId: TestShipments.InclineId, clientName: "ignored — comes from lookup"),
                ShipmentSource.Dispatcher,
                "Dispatch"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var shipment = Assert.Single(shipments.Shipments);
        Assert.StartsWith("SH-", shipment.ShipmentNumber, StringComparison.Ordinal);

        // The caller's name is discarded: it is snapshotted from the lookup, so a mistyped name
        // can never disagree with the id it claims to describe.
        Assert.Equal("Incline Group", shipment.ClientName);
    }

    [Fact]
    public async Task Register_rejects_a_client_that_is_not_in_the_lookup()
    {
        var (shipments, clients, numbers) = Fakes();
        var handler = new RegisterShipmentCommandHandler(shipments, numbers, clients);

        var result = await handler.Handle(
            new RegisterShipmentCommand(
                TestShipments.TenantId,
                TestShipments.Details(clientId: Guid.NewGuid(), clientName: "Nobody"),
                ShipmentSource.Dispatcher,
                "Dispatch"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.ClientNotFound, result.Error);
        Assert.Empty(shipments.Shipments);
    }

    [Fact]
    public async Task Adding_a_leg_takes_the_trips_corridor_and_leaves_its_client_alone()
    {
        // The headline handler-level assertion. The corridor is a convenience default; the payer
        // is not, and there is no code path here that could copy one onto the other.
        var (shipments, clients, _) = Fakes();
        var trips = new FakeTripRepository();

        var alamosTrip = TestPlanning.ScheduleTrip(
            tripNumber: "TR-4824", clientId: TestShipments.AlamosId).Value;
        trips.Trips.Add(alamosTrip);

        var freight = TestShipments.RegisterBillable();
        shipments.Add(freight);

        var handler = new AddShipmentLegCommandHandler(shipments, trips);
        var result = await handler.Handle(
            new AddShipmentLegCommand(freight.Id, alamosTrip.Id, null, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var leg = Assert.Single(freight.Legs);
        Assert.Equal("Thompson", leg.FromName);
        Assert.Equal("Lynn Lake", leg.ToName);
        Assert.Equal(TestShipments.InclineId, freight.ClientId);
        Assert.Equal("Incline Group", freight.ClientName);
        Assert.NotEqual(alamosTrip.ClientId, freight.ClientId);
    }

    [Fact]
    public async Task Adding_a_leg_to_a_run_that_already_happened_is_refused()
    {
        var (shipments, _, _) = Fakes();
        var trips = new FakeTripRepository();

        var trip = TestPlanning.ScheduleTrip(clientId: TestShipments.AlamosId).Value;
        trip.Cancel("Weather");
        trips.Trips.Add(trip);

        var freight = TestShipments.Register();
        shipments.Add(freight);

        var handler = new AddShipmentLegCommandHandler(shipments, trips);
        var result = await handler.Handle(
            new AddShipmentLegCommand(freight.Id, trip.Id, null, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.TripOperationallyClosed, result.Error);
    }

    [Fact]
    public async Task Bulk_assign_reports_per_shipment_failures_instead_of_failing_the_batch()
    {
        // One already-delivered parcel must not block the other two — nine modal round trips is
        // the difference between the feature being used and abandoned.
        var (shipments, _, _) = Fakes();
        var trips = new FakeTripRepository();

        var trip = TestPlanning.ScheduleTrip(clientId: TestShipments.AlamosId).Value;
        trips.Trips.Add(trip);

        var ok1 = TestShipments.Register(shipmentNumber: "SH-1");
        var ok2 = TestShipments.Register(shipmentNumber: "SH-2");
        var alreadyGone = TestShipments.Register(shipmentNumber: "SH-3");
        alreadyGone.RecordDelivery(atUtc: null, receivedBy: "M. Bighetty", note: null);

        shipments.Shipments.AddRange([ok1, ok2, alreadyGone]);

        var handler = new BulkAssignShipmentsCommandHandler(shipments, trips);
        var result = await handler.Handle(
            new BulkAssignShipmentsCommand(trip.Id, [ok1.Id, ok2.Id, alreadyGone.Id]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Assigned);
        var failure = Assert.Single(result.Value.Failures);
        Assert.Equal(alreadyGone.Id, failure.ShipmentId);
        Assert.Equal("Trips.Shipment.OperationallyClosed", failure.Code);
        Assert.Single(ok1.Legs);
        Assert.Empty(alreadyGone.Legs);
    }

    [Fact]
    public async Task Set_billing_attributes_a_legacy_backfilled_shipment_and_starts_its_billing_arc()
    {
        // Exactly the path every row the manifest migration produces has to travel: delivered,
        // clientless, unbillable — until a dispatcher says who it was for.
        var (shipments, clients, _) = Fakes();
        var legacy = TestShipments.Register();
        legacy.RecordDelivery(atUtc: null, receivedBy: null, note: null);
        shipments.Add(legacy);
        Assert.Equal(ShipmentStatus.Delivered, legacy.Status);

        var handler = new SetShipmentBillingCommandHandler(shipments, clients);
        var result = await handler.Handle(
            new SetShipmentBillingCommand(legacy.Id, TestShipments.InclineId, "PO-77", 250m, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.ReadyForBilling, legacy.Status);
        Assert.Equal("Incline Group", legacy.ClientName);
    }

    private static (FakeShipmentRepository, FakeClientLookupRepository, FakeShipmentNumberGenerator) Fakes()
    {
        var clients = new FakeClientLookupRepository();
        clients.Clients.Add(new ClientLookup
        {
            ClientId = TestShipments.InclineId,
            TenantId = TestShipments.TenantId,
            Name = "Incline Group",
            Type = "Client",
            Tag = "incline",
        });
        clients.Clients.Add(new ClientLookup
        {
            ClientId = TestShipments.AlamosId,
            TenantId = TestShipments.TenantId,
            Name = "Alamos Gold",
            Type = "Client",
            Tag = "alamos",
        });

        return (new FakeShipmentRepository(), clients, new FakeShipmentNumberGenerator());
    }
}
