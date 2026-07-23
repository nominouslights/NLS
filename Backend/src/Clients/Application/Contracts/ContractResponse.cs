namespace NorthernLink.Clients.Application.Contracts;

/// <summary>
/// The Clients module's public representation of a contract. BillingModel
/// ("RoundTripRate", "Manual"), BillingFrequency ("Monthly", "BiWeekly", "Weekly"), and
/// Status ("Active", "Ended", "Terminated") are enum names as strings.
/// </summary>
public sealed record ContractResponse(
    Guid Id,
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
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
