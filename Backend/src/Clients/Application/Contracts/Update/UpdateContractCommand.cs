using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Domain.Contracts;

namespace NorthernLink.Clients.Application.Contracts.Update;

/// <summary>Amends an active contract's terms (same overlap rule as creation).</summary>
public sealed record UpdateContractCommand(
    Guid TenantId,
    Guid ContractId,
    DateOnly StartDate,
    DateOnly? EndDate,
    BillingModel BillingModel,
    decimal? RatePerRoundTripCad,
    bool GstApplicable,
    string? BudgetCode,
    BillingFrequency BillingFrequency,
    int NetTermsDays,
    string? DefaultPoNumber) : ICommand;
