using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.Vehicles;

public sealed record SetVehicleStatusCommand(
    Guid Id,
    VehicleStatus Status,
    string? StatusNote) : ICommand;
