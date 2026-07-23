using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.BillableTrips;

/// <summary>
/// Billing's replica of a completed trip, inserted-if-absent from
/// <c>TripCompletedIntegrationEvent</c> keyed on <see cref="Id"/> (the Trips module's
/// TripId) — the pool draft invoices draw from. <see cref="InvoiceId"/> is the claim:
/// null while uninvoiced, stamped when a draft's line prices the trip, cleared when the
/// line is removed or the draft voided — so a trip can never be billed twice.
/// <see cref="RoundTripKey"/> pairs a template-generated outbound leg with its return
/// leg; ad-hoc trips carry null and are only ever invoiced via manual lines.
/// </summary>
public sealed class BillableTrip : ITenantScoped
{
    /// <summary>The Trips module's TripId — the insert-if-absent key.</summary>
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    public string TripNumber { get; set; } = null!;
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string ServiceType { get; set; } = null!;
    public string RouteName { get; set; } = null!;
    public string Origin { get; set; } = null!;
    public string Destination { get; set; } = null!;
    public int DistanceKm { get; set; }
    public DateOnly ServiceDate { get; set; }
    public string? RoundTripKey { get; set; }
    public string? PoNumber { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }

    /// <summary>The invoice currently claiming this trip; null while uninvoiced.</summary>
    public Guid? InvoiceId { get; set; }

    public bool IsUninvoiced => InvoiceId is null;

    public void ClaimFor(Guid invoiceId) => InvoiceId = invoiceId;

    public void Release() => InvoiceId = null;
}
