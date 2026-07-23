using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Contracts;

/// <summary>
/// Billing's replica of a client contract, maintained by upserting
/// <c>ContractChangedIntegrationEvent</c> payloads keyed on <see cref="Id"/> (the Clients
/// module's ContractId) — never by querying Clients. A plain replica row, not an
/// aggregate: Clients owns the contract's lifecycle; Billing only reads it to price
/// draft invoices and default their PO/budget/terms fields. String-typed
/// <see cref="BillingModel"/> / <see cref="BillingFrequency"/> / <see cref="Status"/>
/// mirror the event payload verbatim (integration events never carry another module's
/// enums).
/// </summary>
public sealed class ContractSnapshot : ITenantScoped
{
    /// <summary>The only billing model draft generation auto-prices; everything else is manual lines.</summary>
    public const string RoundTripRateBillingModel = "RoundTripRate";

    /// <summary>The Clients module's ContractId — the upsert key.</summary>
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string BillingModel { get; set; } = null!;
    public decimal? RatePerRoundTripCad { get; set; }
    public bool GstApplicable { get; set; }
    public string? BudgetCode { get; set; }
    public string BillingFrequency { get; set; } = null!;
    public int NetTermsDays { get; set; }
    public string? DefaultPoNumber { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Whether the contract's own term covers any part of the given period.</summary>
    public bool Covers(DateOnly periodStart, DateOnly periodEnd) =>
        StartDate <= periodEnd && (EndDate is null || EndDate >= periodStart);
}
