using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

public sealed record AddCargoItemCommand(
    Guid TripId,
    CargoType CargoType,
    string? Description,
    int Quantity) : ICommand<AddCargoItemResult>;

public sealed record AddCargoItemResult(Guid CargoItemId);
