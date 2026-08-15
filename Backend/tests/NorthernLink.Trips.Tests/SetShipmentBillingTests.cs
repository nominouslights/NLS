using NorthernLink.Trips.Domain.Shipments;
using NorthernLink.Trips.Domain.Shipments.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Attaching, correcting, and clearing the party who pays. This is also the path every backfilled
/// legacy cargo row has to travel: the old manifest jsonb never recorded a client, so those rows
/// land clientless and unbillable until a dispatcher attributes them by hand.
/// </summary>
public class SetShipmentBillingTests
{
    [Fact]
    public void Attaching_a_client_to_a_delivered_shipment_makes_it_billable_and_raises_the_feed()
    {
        // The legacy-backlog path: delivered months ago, client recorded nowhere, charge unknown.
        var shipment = TestShipments.Register();
        shipment.RecordDelivery(atUtc: null, receivedBy: "M. Bighetty", note: null);
        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
        shipment.ClearDomainEvents();

        var result = shipment.SetBilling(
            TestShipments.InclineId, "Incline Group", "PO-77", chargeCad: 250m, paymentMethod: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.ReadyForBilling, shipment.Status);
        Assert.Equal(TestShipments.InclineId, shipment.ClientId);
        Assert.Equal(250m, shipment.ChargeCad);
        Assert.IsType<ShipmentReadyForBillingDomainEvent>(Assert.Single(shipment.DomainEvents));
    }

    [Fact]
    public void Correcting_the_charge_before_an_invoice_claims_it_re_raises_the_feed()
    {
        // Billing refreshes an uninvoiced replica row off the repeat, so the corrected number is
        // what any later draft prices.
        var shipment = Ready();
        shipment.ClearDomainEvents();

        var result = shipment.SetBilling(
            TestShipments.InclineId, "Incline Group", null, chargeCad: 310m, paymentMethod: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.ReadyForBilling, shipment.Status);
        Assert.Equal(310m, shipment.ChargeCad);
        Assert.IsType<ShipmentReadyForBillingDomainEvent>(Assert.Single(shipment.DomainEvents));
    }

    [Fact]
    public void Clearing_a_client_attached_in_error_falls_back_to_delivered()
    {
        var shipment = Ready();
        shipment.ClearDomainEvents();

        var result = shipment.SetBilling(null, null, null, chargeCad: null, paymentMethod: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
        Assert.Null(shipment.ClientId);
        Assert.IsType<ShipmentDeliveredDomainEvent>(Assert.Single(shipment.DomainEvents));
    }

    [Fact]
    public void Billing_cannot_be_changed_once_an_invoice_speaks_for_the_shipment()
    {
        // Repricing an invoiced shipment is a line edit on the invoice, not a Trips-side change.
        var shipment = Ready();
        shipment.MarkInvoiced();

        var result = shipment.SetBilling(
            TestShipments.InclineId, "Incline Group", null, chargeCad: 999m, paymentMethod: null);

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.BillingNotEditable, result.Error);
        Assert.Equal(250m, shipment.ChargeCad);
    }

    [Fact]
    public void Descriptive_edits_stay_legal_on_an_invoiced_shipment()
    {
        // Correcting a consignee's name on an invoiced parcel is routine and touches no money.
        var shipment = Ready();
        shipment.MarkInvoiced();

        var result = shipment.UpdateDetails(
            TestShipments.Details(
                description: "Pallet of dry goods — corrected",
                clientId: TestShipments.InclineId,
                clientName: "Incline Group",
                chargeCad: 250m),
            ShipmentSource.Dispatcher,
            "Dispatch");

        Assert.True(result.IsSuccess);
        Assert.Equal("Pallet of dry goods — corrected", shipment.Description);
        Assert.Equal(ShipmentStatus.Invoiced, shipment.Status);
    }

    [Fact]
    public void An_update_that_moves_the_money_on_an_invoiced_shipment_is_refused()
    {
        var shipment = Ready();
        shipment.MarkInvoiced();

        var result = shipment.UpdateDetails(
            TestShipments.Details(clientId: TestShipments.InclineId, clientName: "Incline Group", chargeCad: 999m),
            ShipmentSource.Dispatcher,
            "Dispatch");

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.BillingNotEditable, result.Error);
    }

    [Fact]
    public void Attaching_a_client_without_a_name_snapshot_is_refused()
    {
        // Snapshot semantics, exactly as Trip.AssignDriver requires the driver's name.
        var shipment = TestShipments.Register();

        var result = shipment.SetBilling(TestShipments.InclineId, "  ", null, 250m, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.ClientNameRequired, result.Error);
    }

    [Fact]
    public void Attaching_a_client_to_an_undelivered_shipment_changes_no_status()
    {
        // Pre-registration knows who pays long before the goods move; the billing arc still
        // starts at handover, not here.
        var shipment = TestShipments.Register();

        var result = shipment.SetBilling(TestShipments.InclineId, "Incline Group", null, 250m, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.Registered, shipment.Status);
        Assert.True(shipment.IsBillable);
        Assert.DoesNotContain(shipment.DomainEvents, e => e is ShipmentReadyForBillingDomainEvent);
    }

    private static Shipment Ready()
    {
        var shipment = TestShipments.RegisterBillable();
        shipment.RecordDelivery(atUtc: null, receivedBy: "M. Bighetty", note: null);
        return shipment;
    }
}
