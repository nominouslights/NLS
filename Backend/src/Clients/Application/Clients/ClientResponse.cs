namespace NorthernLink.Clients.Application.Clients;

/// <summary>
/// The Clients module's public representation of a client organization — the shape every
/// frontend consumes. <paramref name="Type"/> is the <c>ClientType</c> name as a string
/// (Client or VendorPartner). <paramref name="ServiceType"/> is the <c>ClientServiceType</c>
/// name as a string (ContractCrew, Community, Nihb, Charter, Cargo, Grocery) — null for
/// VendorPartner organizations. <paramref name="ActiveContract"/> embeds the client's current
/// contract summary so the client profile screen needs one call; null when the client has no
/// active contract.
/// </summary>
public sealed record ClientResponse(
    Guid Id,
    string Name,
    string Type,
    string? ServiceType,
    string Tag,
    string? AgreementReference,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    ActiveContractSummary? ActiveContract);

/// <summary>
/// The active-contract slice of <see cref="ClientResponse"/>: the contract currently in
/// force (or, when none covers today, the next upcoming active contract). BillingModel and
/// BillingFrequency are enum names as strings.
/// </summary>
public sealed record ActiveContractSummary(
    Guid ActiveContractId,
    DateOnly StartDate,
    DateOnly? EndDate,
    string BillingModel,
    decimal? RatePerRoundTripCad,
    bool GstApplicable,
    string? BudgetCode,
    string BillingFrequency,
    int NetTermsDays,
    string? DefaultPoNumber);
