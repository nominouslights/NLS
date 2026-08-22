using NorthernLink.Trips.Domain.Shipments;
using NorthernLink.Trips.Domain.Shipments.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The invariant the whole cargo design exists to protect: <b>a shipment's client is its own and
/// is never the trip's.</b> A run for Alamos is routinely full of Incline Group's freight, and
/// one trip can carry parcels for several clients at once.
/// <para>
/// If any of these fail, cargo is being invoiced to the wrong company — the single worst
/// outcome this feature can produce, and one that leaves no trace in the data to detect later.
/// </para>
/// </summary>
public class ShipmentBillingClientTests
{
    [Fact]
    public void Routing_a_shipment_through_a_trip_never_touches_its_client()
    {
        var alamosTrip = TestPlanning.ScheduleTrip(
            tripNumber: "TR-4824", clientId: TestShipments.AlamosId).Value;
        var inclineFreight = TestShipments.RegisterBillable();

        var result = inclineFreight.AddLeg(
            alamosTrip.Id,
            alamosTrip.TripNumber,
            alamosTrip.ServiceDate,
            fromStopId: null,
            fromName: "Thompson",
            toStopId: null,
            toName: "Lynn Lake");

        Assert.True(result.IsSuccess);
        Assert.Equal(TestShipments.AlamosId, alamosTrip.ClientId);
        Assert.Equal(TestShipments.InclineId, inclineFreight.ClientId);
        Assert.Equal("Incline Group", inclineFreight.ClientName);
        Assert.NotEqual(alamosTrip.ClientId, inclineFreight.ClientId);
    }

    [Fact]
    public void The_client_survives_the_entire_operational_lifecycle()
    {
        var alamosTrip = TestPlanning.ScheduleTrip(
            tripNumber: "TR-4824", clientId: TestShipments.AlamosId).Value;
        var hubRun = TestPlanning.ScheduleTrip(
            tripNumber: "TR-4901", clientId: TestShipments.AlamosId).Value;
        var freight = TestShipments.RegisterBillable();

        freight.AddLeg(alamosTrip.Id, alamosTrip.TripNumber, alamosTrip.ServiceDate, null, "Thompson", null, "Leaf Rapids");
        AssertStillIncline(freight);

        freight.RecordLegPickup(1, atUtc: null, by: "J. Spence");
        AssertStillIncline(freight);

        freight.RecordLegDrop(1, atUtc: null, by: "J. Spence");
        AssertStillIncline(freight);

        freight.AddLeg(hubRun.Id, hubRun.TripNumber, hubRun.ServiceDate, null, "Leaf Rapids", null, "Lynn Lake");
        AssertStillIncline(freight);

        freight.RecordLegPickup(2, atUtc: null, by: "D. Moose");
        freight.RecordDelivery(atUtc: null, receivedBy: "M. Bighetty", note: null);
        AssertStillIncline(freight);

        Assert.Equal(ShipmentStatus.ReadyForBilling, freight.Status);
    }

    [Fact]
    public void Releasing_a_shipment_from_a_cancelled_trip_never_touches_its_client()
    {
        var alamosTrip = TestPlanning.ScheduleTrip(
            tripNumber: "TR-4824", clientId: TestShipments.AlamosId).Value;
        var freight = TestShipments.RegisterBillable();
        freight.AddLeg(alamosTrip.Id, alamosTrip.TripNumber, alamosTrip.ServiceDate, null, "Thompson", null, "Lynn Lake");

        freight.ReleaseFromTrip(alamosTrip.Id);

        AssertStillIncline(freight);
        Assert.Equal(ShipmentStatus.Registered, freight.Status);
    }

    [Fact]
    public void One_trip_carries_freight_for_several_different_clients_at_once()
    {
        // The shape the old jsonb model could not represent at all: four anonymous rows on a
        // manifest with no client between them.
        var alamosTrip = TestPlanning.ScheduleTrip(
            tripNumber: "TR-4824", clientId: TestShipments.AlamosId).Value;

        var forIncline = TestShipments.Register(
            TestShipments.Details(clientId: TestShipments.InclineId, clientName: "Incline Group", chargeCad: 250m),
            "SH-1001");
        var forAlamos = TestShipments.Register(
            TestShipments.Details(clientId: TestShipments.AlamosId, clientName: "Alamos Gold", chargeCad: 90m),
            "SH-1002");
        var counterSale = TestShipments.Register(
            TestShipments.Details(chargeCad: 18m, paymentMethod: ShipmentPaymentMethod.Cash),
            "SH-1003");

        foreach (var shipment in new[] { forIncline, forAlamos, counterSale })
        {
            var result = shipment.AddLeg(
                alamosTrip.Id, alamosTrip.TripNumber, alamosTrip.ServiceDate, null, "Thompson", null, "Lynn Lake");
            Assert.True(result.IsSuccess);
        }

        Assert.Equal(TestShipments.InclineId, forIncline.ClientId);
        Assert.Equal(TestShipments.AlamosId, forAlamos.ClientId);
        Assert.Null(counterSale.ClientId);

        // Only the two with a client and a charge start a billing arc; the counter sale is done.
        foreach (var shipment in new[] { forIncline, forAlamos, counterSale })
        {
            shipment.RecordDelivery(atUtc: null, receivedBy: "M. Bighetty", note: null);
        }

        Assert.Equal(ShipmentStatus.ReadyForBilling, forIncline.Status);
        Assert.Equal(ShipmentStatus.ReadyForBilling, forAlamos.Status);
        Assert.Equal(ShipmentStatus.Delivered, counterSale.Status);
    }

    [Fact]
    public void The_billing_feed_carries_the_shipments_client_not_the_trips()
    {
        var alamosTrip = TestPlanning.ScheduleTrip(
            tripNumber: "TR-4824", clientId: TestShipments.AlamosId).Value;
        var freight = TestShipments.RegisterBillable();
        freight.AddLeg(alamosTrip.Id, alamosTrip.TripNumber, alamosTrip.ServiceDate, null, "Thompson", null, "Lynn Lake");
        freight.ClearDomainEvents();

        freight.RecordDelivery(atUtc: null, receivedBy: "M. Bighetty", note: null);

        // The event itself carries only the id; what matters is that the aggregate it will be
        // mapped from still holds Incline, on a trip that belongs to Alamos.
        var ready = Assert.IsType<ShipmentReadyForBillingDomainEvent>(Assert.Single(freight.DomainEvents));
        Assert.Equal(freight.Id, ready.ShipmentId);
        Assert.Equal(TestShipments.InclineId, freight.ClientId);
        Assert.Equal("TR-4824", Assert.Single(freight.Legs).TripNumber);
        Assert.Equal(TestShipments.AlamosId, alamosTrip.ClientId);
    }

    private static void AssertStillIncline(Shipment freight)
    {
        Assert.Equal(TestShipments.InclineId, freight.ClientId);
        Assert.Equal("Incline Group", freight.ClientName);
    }
}
