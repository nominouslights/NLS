using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

public sealed record AddCargoItemCommand(
    Guid TripId,
    CargoType CargoType,
    string? Description,
    int Quantity,
    decimal? WeightKg = null,
    decimal? Charge = null,
    bool IsHazmat = false,
    bool IsSecured = false) : ICommand<AddCargoItemResult>;

public sealed record AddCargoItemResult(Guid CargoItemId);
