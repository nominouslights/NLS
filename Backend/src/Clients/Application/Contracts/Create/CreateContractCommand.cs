using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Domain.Contracts;

namespace NorthernLink.Clients.Application.Contracts.Create;

/// <summary>
/// Creates a new (immediately active) contract for a client. Returns the new contract's id.
/// Rejected with <c>Clients.Contract.OverlappingPeriod</c> when the client already has an
/// active contract whose inclusive period intersects this one.
/// </summary>
public sealed record CreateContractCommand(
    Guid TenantId,
    Guid ClientId,
    DateOnly StartDate,
    DateOnly? EndDate,
    BillingModel BillingModel,
    decimal? RatePerRoundTripCad,
    bool GstApplicable,
    string? BudgetCode,
    BillingFrequency BillingFrequency,
    int NetTermsDays,
    string? DefaultPoNumber) : ICommand<Guid>;
