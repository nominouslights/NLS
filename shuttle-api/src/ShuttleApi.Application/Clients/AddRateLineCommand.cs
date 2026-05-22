using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Clients;

public sealed record AddRateLineCommand(
    Guid ContractId,
    string BillingCode,
    string Description,
    string VehicleType,
    int? MaxDistanceKm,
    bool CargoIncluded,
    decimal DayRate) : ICommand<AddRateLineResult>;

public sealed record AddRateLineResult(Guid RateLineId);
