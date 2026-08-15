using NorthernLink.Trips.Domain.Shipments;
using NorthernLink.Trips.Domain.Shipments.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Multi-leg routing: a shipment can ride several trips to reach the consignee — out on one run,
/// transferred at a hub, onward on the next — while staying one billable item.
/// </summary>
public class ShipmentLegTests
{
    private static readonly Guid FirstTrip = Guid.Parse("00000000-0000-0000-0000-00000000f001");
    private static readonly Guid SecondTrip = Guid.Parse("00000000-0000-0000-0000-00000000f002");

    [Fact]
    public void Adding_the_first_leg_moves_a_registered_shipment_to_assigned()
    {
        var shipment = TestShipments.Register();
        shipment.ClearDomainEvents();

        var result = shipment.AddLeg(
            FirstTrip, "TR-1001", new DateOnly(2026, 7, 21), null, "Thompson", null, "Leaf Rapids");

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.Assigned, shipment.Status);
        var leg = Assert.Single(shipment.Legs);
        Assert.Equal(1, leg.Sequence);
        Assert.Equal("TR-1001", leg.TripNumber);
        Assert.Equal(new DateOnly(2026, 7, 21), leg.TripServiceDate);
        Assert.Equal(ShipmentLegStatus.Planned, leg.Status);
        Assert.IsType<ShipmentRoutingChangedDomainEvent>(Assert.Single(shipment.DomainEvents));
    }

    [Fact]
    public void Legs_are_appended_in_order()
    {
        var shipment = TestShipments.Register()
            .OnTrip(FirstTrip, "TR-1001", from: "Thompson", to: "Leaf Rapids")
            .OnTrip(SecondTrip, "TR-1002", from: "Leaf Rapids", to: "Lynn Lake");

        Assert.Equal([1, 2], shipment.Legs.Select(l => l.Sequence));
        Assert.Equal(["TR-1001", "TR-1002"], shipment.Legs.Select(l => l.TripNumber));
    }

    [Fact]
    public void The_same_trip_cannot_carry_a_shipment_twice()
    {
        // A single run cannot both take the goods out and receive them onward — always a mis-click.
        var shipment = TestShipments.Register().OnTrip(FirstTrip);

        var result = shipment.AddLeg(
            FirstTrip, "TR-1001", new DateOnly(2026, 7, 21), null, "Thompson", null, "Lynn Lake");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.LegAlreadyOnTrip, result.Error);
    }

    [Fact]
    public void Removing_the_only_planned_leg_returns_the_shipment_to_the_unassigned_pool()
    {
        var shipment = TestShipments.Register().OnTrip(FirstTrip);

        var result = shipment.RemoveLeg(1);

        Assert.True(result.IsSuccess);
        Assert.Empty(shipment.Legs);
        Assert.Equal(ShipmentStatus.Registered, shipment.Status);
    }

    [Fact]
    public void Removing_a_leg_closes_the_numbering_gap()
    {
        var shipment = TestShipments.Register()
            .OnTrip(FirstTrip, "TR-1001")
            .OnTrip(SecondTrip, "TR-1002");

        shipment.RemoveLeg(1);

        var leg = Assert.Single(shipment.Legs);
        Assert.Equal(1, leg.Sequence);
        Assert.Equal("TR-1002", leg.TripNumber);
    }

    [Fact]
    public void A_leg_that_has_already_been_picked_up_stays_on_the_record()
    {
        // The freight physically moved. That is history, not a plan to be edited away.
        var shipment = TestShipments.Register().OnTrip(FirstTrip);
        shipment.RecordLegPickup(1, atUtc: null, by: "J. Spence");

        var result = shipment.RemoveLeg(1);

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.LegNotRemovable, result.Error);
    }

    [Fact]
    public void Picking_up_the_first_leg_puts_the_shipment_in_transit()
    {
        var shipment = TestShipments.Register().OnTrip(FirstTrip);
        shipment.ClearDomainEvents();

        var result = shipment.RecordLegPickup(1, atUtc: null, by: "J. Spence");

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
        Assert.Equal("J. Spence", shipment.Legs[0].PickedUpBy);
        Assert.NotNull(shipment.Legs[0].PickedUpAtUtc);
        Assert.IsType<ShipmentLegPickedUpDomainEvent>(Assert.Single(shipment.DomainEvents));
    }

    [Fact]
    public void Legs_run_in_order()
    {
        var shipment = TestShipments.Register()
            .OnTrip(FirstTrip, "TR-1001")
            .OnTrip(SecondTrip, "TR-1002");

        var result = shipment.RecordLegPickup(2, atUtc: null, by: "J. Spence");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.EarlierLegOutstanding, result.Error);
    }

    [Fact]
    public void Freight_dropped_at_a_hub_with_an_onward_leg_reads_as_awaiting_transfer()
    {
        // The state the old model could not express at all — real goods sitting in a real
        // building. Invisible inventory unless a screen can ask for exactly this.
        var shipment = TestShipments.Register()
            .OnTrip(FirstTrip, "TR-1001", from: "Thompson", to: "Leaf Rapids")
            .OnTrip(SecondTrip, "TR-1002", from: "Leaf Rapids", to: "Lynn Lake");

        shipment.RecordLegPickup(1, atUtc: null, by: "J. Spence");
        shipment.RecordLegDrop(1, atUtc: null, by: "J. Spence");

        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
        Assert.True(shipment.IsAwaitingTransfer);
        Assert.Equal("TR-1002", shipment.CurrentLeg?.TripNumber);
    }

    [Fact]
    public void Once_the_onward_leg_is_loaded_it_is_no_longer_awaiting_transfer()
    {
        var shipment = TestShipments.Register()
            .OnTrip(FirstTrip, "TR-1001")
            .OnTrip(SecondTrip, "TR-1002");

        shipment.RecordLegPickup(1, atUtc: null, by: "J. Spence");
        shipment.RecordLegDrop(1, atUtc: null, by: "J. Spence");
        shipment.RecordLegPickup(2, atUtc: null, by: "D. Moose");

        Assert.False(shipment.IsAwaitingTransfer);
    }

    [Fact]
    public void Only_a_picked_up_leg_can_be_dropped()
    {
        var shipment = TestShipments.Register().OnTrip(FirstTrip);

        var result = shipment.RecordLegDrop(1, atUtc: null, by: "J. Spence");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.LegNotPickedUp, result.Error);
    }

    [Fact]
    public void Delivery_closes_out_any_leg_still_outstanding()
    {
        // A dispatcher asserting the consignee has the goods makes a remaining leg moot by
        // definition — the same forgiveness Trip.FinishOperations extends to a forgotten START.
        var shipment = TestShipments.Register()
            .OnTrip(FirstTrip, "TR-1001")
            .OnTrip(SecondTrip, "TR-1002");

        shipment.RecordLegPickup(1, atUtc: null, by: "J. Spence");
        var result = shipment.RecordDelivery(atUtc: null, receivedBy: "M. Bighetty", note: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
        Assert.All(shipment.Legs, l => Assert.Equal(ShipmentLegStatus.Dropped, l.Status));
    }

    [Fact]
    public void A_cancelled_trip_releases_its_planned_legs_but_never_the_freight()
    {
        // The goods still need to get there — back in the queue is the dispatcher's next action.
        var shipment = TestShipments.Register().OnTrip(FirstTrip);

        var result = shipment.ReleaseFromTrip(FirstTrip);

        Assert.True(result.IsSuccess);
        Assert.Empty(shipment.Legs);
        Assert.Equal(ShipmentStatus.Registered, shipment.Status);
        Assert.NotEqual(ShipmentStatus.Cancelled, shipment.Status);
    }

    [Fact]
    public void Releasing_a_trip_leaves_legs_the_freight_actually_travelled_alone()
    {
        var shipment = TestShipments.Register()
            .OnTrip(FirstTrip, "TR-1001")
            .OnTrip(SecondTrip, "TR-1002");

        shipment.RecordLegPickup(1, atUtc: null, by: "J. Spence");
        shipment.RecordLegDrop(1, atUtc: null, by: "J. Spence");

        shipment.ReleaseFromTrip(SecondTrip);

        // Stranded at the hub: leg 1 is history, leg 2 is gone, and the goods are still somewhere.
        var leg = Assert.Single(shipment.Legs);
        Assert.Equal("TR-1001", leg.TripNumber);
        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
        Assert.False(shipment.IsAwaitingTransfer);
    }

    [Fact]
    public void Releasing_a_trip_that_carries_nothing_is_a_no_op()
    {
        // At-least-once delivery means this reaction runs twice sooner or later.
        var shipment = TestShipments.Register().OnTrip(FirstTrip);
        shipment.ReleaseFromTrip(FirstTrip);
        shipment.ClearDomainEvents();

        var result = shipment.ReleaseFromTrip(FirstTrip);

        Assert.True(result.IsSuccess);
        Assert.Empty(shipment.DomainEvents);
    }

    [Fact]
    public void Cargo_can_ride_a_deadhead()
    {
        // A deadhead is an empty PASSENGER leg. Filling a repositioning run with freight is the
        // entire economic point of having one — the asymmetry with the manifest guard is intended.
        var deadhead = TestPlanning.ScheduleTrip(
            tripNumber: "TR-1099", clientId: TestShipments.AlamosId, isEmptyLeg: true).Value;
        var shipment = TestShipments.Register();

        var result = shipment.AddLeg(
            deadhead.Id, deadhead.TripNumber, deadhead.ServiceDate, null, "Lynn Lake", null, "Thompson");

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.Assigned, shipment.Status);
    }

    [Fact]
    public void Routing_cannot_change_once_the_goods_are_handed_over()
    {
        var shipment = TestShipments.Register().OnTrip(FirstTrip);
        shipment.RecordDelivery(atUtc: null, receivedBy: "M. Bighetty", note: null);

        var result = shipment.AddLeg(
            SecondTrip, "TR-1002", new DateOnly(2026, 7, 22), null, "Lynn Lake", null, "Thompson");

        Assert.True(result.IsFailure);
        Assert.Equal("Trips.Shipment.OperationallyClosed", result.Error.Code);
    }
}
