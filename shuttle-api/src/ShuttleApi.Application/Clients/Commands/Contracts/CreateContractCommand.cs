using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Clients;

public sealed record CreateContractCommand(
    Guid ClientId,
    DateTime StartDate,
    DateTime RenewalDate,
    string? Notes,
    IReadOnlyList<RateLineDto> RateLines) : ICommand<CreateContractResult>;

public sealed record RateLineDto(
    string BillingCode,
    string Description,
    string VehicleType,
    int? MaxDistanceKm,
    bool CargoIncluded,
    decimal DayRate);

public sealed record CreateContractResult(Guid ContractId);
