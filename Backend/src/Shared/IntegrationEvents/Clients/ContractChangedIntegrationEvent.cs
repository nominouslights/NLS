using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Clients;

/// <summary>
/// Published whenever a client contract is created, amended, or terminated — routing
/// key <c>clients.contract-changed</c>. Carries the full billing-relevant snapshot so
/// Billing can maintain its <c>contract_snapshots</c> replica by upserting on
/// <see cref="ContractId"/> (idempotent under at-least-once delivery) and never needs a
/// library reference to Clients. The contract on the client profile is the source of
/// truth for invoicing: draft invoices price round trips from
/// <see cref="RatePerRoundTripCad"/> and default their PO/budget/terms fields from
/// here. BillingModel ("RoundTripRate", "Manual"), BillingFrequency ("Monthly",
/// "BiWeekly", "Weekly"), and Status ("Active", "Ended", "Terminated") travel as
/// strings: integration events never reference Clients' internal enums.
/// <see cref="TenantId"/> is part of the payload because handlers run outside any
/// HTTP request.
/// </summary>
public sealed record ContractChangedIntegrationEvent(
    Guid ContractId,
    Guid TenantId,
    Guid ClientId,
    string ClientName,
    DateOnly StartDate,
    DateOnly? EndDate,
    string BillingModel,
    decimal? RatePerRoundTripCad,
    bool GstApplicable,
    string? BudgetCode,
    string BillingFrequency,
    int NetTermsDays,
    string? DefaultPoNumber,
    string Status) : IntegrationEvent;
