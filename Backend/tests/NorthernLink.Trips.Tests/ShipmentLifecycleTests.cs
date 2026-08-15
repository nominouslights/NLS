using NorthernLink.Trips.Domain.Shipments;
using NorthernLink.Trips.Domain.Shipments.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class ShipmentLifecycleTests
{
    /// <summary>
    /// The complete allowed edge set — 19 edges, everything else forbidden. Kept as an explicit
    /// literal so a change to <see cref="Shipment.CanTransition"/> has to be mirrored here on
    /// purpose, and checked as a full cross-product so a newly added enum member fails loudly
    /// instead of landing quietly in the matrix's discard arm.
    /// </summary>
    private static readonly HashSet<(ShipmentStatus From, ShipmentStatus To)> AllowedEdges =
    [
        (ShipmentStatus.Registered, ShipmentStatus.Assigned),
        (ShipmentStatus.Registered, ShipmentStatus.Delivered),
        (ShipmentStatus.Registered, ShipmentStatus.ReadyForBilling),
        (ShipmentStatus.Registered, ShipmentStatus.Cancelled),
        (ShipmentStatus.Assigned, ShipmentStatus.Registered),
        (ShipmentStatus.Assigned, ShipmentStatus.InTransit),
        (ShipmentStatus.Assigned, ShipmentStatus.Delivered),
        (ShipmentStatus.Assigned, ShipmentStatus.ReadyForBilling),
        (ShipmentStatus.Assigned, ShipmentStatus.Cancelled),
        (ShipmentStatus.InTransit, ShipmentStatus.Delivered),
        (ShipmentStatus.InTransit, ShipmentStatus.ReadyForBilling),
        (ShipmentStatus.InTransit, ShipmentStatus.Cancelled),
        (ShipmentStatus.Delivered, ShipmentStatus.ReadyForBilling),
        (ShipmentStatus.ReadyForBilling, ShipmentStatus.Delivered),
        (ShipmentStatus.ReadyForBilling, ShipmentStatus.Invoiced),
        (ShipmentStatus.ReadyForBilling, ShipmentStatus.WrittenOff),
        (ShipmentStatus.Invoiced, ShipmentStatus.Settled),
        (ShipmentStatus.Invoiced, ShipmentStatus.WrittenOff),
        (ShipmentStatus.Settled, ShipmentStatus.Invoiced),
    ];

    [Fact]
    public void Transition_matrix_matches_the_allowed_edge_set_exhaustively()
    {
        foreach (var from in Enum.GetValues<ShipmentStatus>())
        {
            foreach (var to in Enum.GetValues<ShipmentStatus>())
            {
                if (from == to)
                {
                    continue; // the diagonal is not a transition
                }

                Assert.True(
                    AllowedEdges.Contains((from, to)) == Shipment.CanTransition(from, to),
                    $"CanTransition({from}, {to}) disagrees with the allowed edge set.");
            }
        }
    }

    [Fact]
    public void Invoiced_can_never_go_back_to_ready_for_billing()
    {
        // Same rule the trip lifecycle enforces: once a worksheet is in QuickBooks it can be
        // adjusted or written off, never un-sent.
        Assert.False(Shipment.CanTransition(ShipmentStatus.Invoiced, ShipmentStatus.ReadyForBilling));
    }

    [Fact]
    public void Register_needs_nothing_but_a_description_and_starts_unrouted()
    {
        // Pre-registration is the point: cargo turns up at the counter long before anyone knows
        // which run will take it.
        var result = Shipment.Register(
            TestShipments.TenantId,
            "SH-1001",
            new ShipmentDetails { Description = "Three boxes" },
            ShipmentSource.Dispatcher,
            enteredBy: "Dispatch");

        Assert.True(result.IsSuccess);
        var shipment = result.Value;
        Assert.Equal(ShipmentStatus.Registered, shipment.Status);
        Assert.Empty(shipment.Legs);
        Assert.Null(shipment.ClientId);
        Assert.False(shipment.IsBillable);
        Assert.Equal(ShipmentKind.Parcel, shipment.Kind);
        var registered = Assert.IsType<ShipmentRegisteredDomainEvent>(Assert.Single(shipment.DomainEvents));
        Assert.Equal(shipment.Id, registered.ShipmentId);
    }

    [Fact]
    public void A_dispatcher_entered_shipment_must_say_who_entered_it()
    {
        var result = Shipment.Register(
            TestShipments.TenantId,
            "SH-1001",
            TestShipments.Details(),
            ShipmentSource.Dispatcher,
            enteredBy: "   ");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.EnteredByRequired, result.Error);
    }

    [Fact]
    public void Delivering_a_clientless_shipment_lands_in_delivered_and_raises_no_billing_event()
    {
        // A counter sale was settled at the desk — nothing downstream has anything to invoice.
        var shipment = TestShipments.Register(
            TestShipments.Details(chargeCad: 18m, paymentMethod: ShipmentPaymentMethod.Cash));
        shipment.ClearDomainEvents();

        var result = shipment.RecordDelivery(atUtc: null, receivedBy: "M. Bighetty", note: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
        Assert.IsType<ShipmentDeliveredDomainEvent>(Assert.Single(shipment.DomainEvents));
        Assert.DoesNotContain(shipment.DomainEvents, e => e is ShipmentReadyForBillingDomainEvent);
    }

    [Fact]
    public void Delivering_a_billable_shipment_lands_in_ready_for_billing_and_raises_the_feed()
    {
        var shipment = TestShipments.RegisterBillable();
        shipment.ClearDomainEvents();

        var result = shipment.RecordDelivery(atUtc: null, receivedBy: "M. Bighetty", note: "Left at agent");

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.ReadyForBilling, shipment.Status);
        Assert.NotNull(shipment.DeliveredAtUtc);
        Assert.Equal("M. Bighetty", shipment.ReceivedBy);
        Assert.IsType<ShipmentReadyForBillingDomainEvent>(Assert.Single(shipment.DomainEvents));
    }

    [Fact]
    public void A_client_with_no_charge_is_not_billable()
    {
        // Both halves of IsBillable matter — a goodwill carry has nothing to invoice either.
        var shipment = TestShipments.Register(
            TestShipments.Details(clientId: TestShipments.InclineId, clientName: "Incline Group"));

        shipment.RecordDelivery(atUtc: null, receivedBy: null, note: null);

        Assert.False(shipment.IsBillable);
        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
    }

    [Fact]
    public void Billing_driven_transitions_are_idempotent_and_raise_nothing_on_replay()
    {
        // The outbox is at-least-once, so every one of these gets delivered twice sooner or later.
        var shipment = Delivered();
        shipment.MarkInvoiced();
        shipment.ClearDomainEvents();

        var replay = shipment.MarkInvoiced();

        Assert.True(replay.IsSuccess);
        Assert.Equal(ShipmentStatus.Invoiced, shipment.Status);
        Assert.Empty(shipment.DomainEvents);
    }

    [Fact]
    public void A_settled_shipment_can_walk_back_to_invoiced_when_a_payment_is_cleared_in_error()
    {
        var shipment = Delivered();
        shipment.MarkInvoiced();
        shipment.MarkPaid();
        Assert.Equal(ShipmentStatus.Settled, shipment.Status);

        var result = shipment.MarkInvoiced();

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.Invoiced, shipment.Status);
    }

    [Fact]
    public void A_billing_driven_transition_on_a_clientless_shipment_is_refused()
    {
        // A clientless shipment never entered the billing arc, so an invoice speaking for it
        // means a claim set has gone wrong upstream — fail loudly rather than move it.
        var shipment = TestShipments.Register();
        shipment.RecordDelivery(atUtc: null, receivedBy: null, note: null);

        var result = shipment.MarkInvoiced();

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.BillingStateOnClientlessShipment, result.Error);
    }

    [Fact]
    public void Close_without_billing_writes_the_shipment_off_and_tells_billing_to_drop_it()
    {
        var shipment = Delivered();
        shipment.ClearDomainEvents();

        var result = shipment.CloseWithoutBilling("Client has no contract");

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.WrittenOff, shipment.Status);
        Assert.Equal("Client has no contract", shipment.WrittenOffReason);
        Assert.IsType<ShipmentClosedWithoutBillingDomainEvent>(Assert.Single(shipment.DomainEvents));
    }

    [Fact]
    public void Close_without_billing_needs_a_reason()
    {
        var result = Delivered().CloseWithoutBilling("  ");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.WriteOffReasonRequired, result.Error);
    }

    [Fact]
    public void A_written_off_shipment_can_no_longer_be_edited()
    {
        var shipment = Delivered();
        shipment.CloseWithoutBilling("Goodwill");

        var result = shipment.UpdateDetails(TestShipments.Details(), ShipmentSource.Dispatcher, "Dispatch");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.NotEditable, result.Error);
    }

    [Fact]
    public void A_collected_amount_with_no_payment_method_is_refused()
    {
        // Half-recorded money is worse than none: it cannot be reconciled either way.
        var result = Shipment.Register(
            TestShipments.TenantId,
            "SH-1001",
            TestShipments.Details(chargeCad: 18m),
            ShipmentSource.Dispatcher,
            "Dispatch");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.PaymentMethodRequired, result.Error);
    }

    [Fact]
    public void A_counter_payment_method_on_a_client_shipment_is_refused()
    {
        // A client's freight settles by invoice; a counter method there is a data-entry mistake
        // that would otherwise make the shipment look paid twice.
        var result = Shipment.Register(
            TestShipments.TenantId,
            "SH-1001",
            TestShipments.Details(
                clientId: TestShipments.InclineId,
                clientName: "Incline Group",
                chargeCad: 250m,
                paymentMethod: ShipmentPaymentMethod.Cash),
            ShipmentSource.Dispatcher,
            "Dispatch");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.PaymentMethodOnClientShipment, result.Error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void A_shipment_needs_at_least_one_piece(int pieces)
    {
        var result = Shipment.Register(
            TestShipments.TenantId,
            "SH-1001",
            TestShipments.Details(pieces: pieces),
            ShipmentSource.Dispatcher,
            "Dispatch");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.InvalidPieces, result.Error);
    }

    [Fact]
    public void A_charge_with_more_than_two_decimal_places_is_refused()
    {
        var result = Shipment.Register(
            TestShipments.TenantId,
            "SH-1001",
            TestShipments.Details(clientId: TestShipments.InclineId, clientName: "Incline Group", chargeCad: 12.345m),
            ShipmentSource.Dispatcher,
            "Dispatch");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.InvalidCharge, result.Error);
    }

    [Fact]
    public void A_required_by_date_before_the_ready_date_is_refused()
    {
        var result = Shipment.Register(
            TestShipments.TenantId,
            "SH-1001",
            TestShipments.Details(
                readyDate: new DateOnly(2026, 7, 21),
                requiredByDate: new DateOnly(2026, 7, 20)),
            ShipmentSource.Dispatcher,
            "Dispatch");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.RequiredByBeforeReady, result.Error);
    }

    private static Shipment Delivered()
    {
        var shipment = TestShipments.RegisterBillable();
        shipment.RecordDelivery(atUtc: null, receivedBy: "M. Bighetty", note: null);
        return shipment;
    }
}
