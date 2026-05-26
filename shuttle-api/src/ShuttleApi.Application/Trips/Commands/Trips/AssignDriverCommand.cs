using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record AssignDriverCommand(
    Guid TripId,
    Guid DriverId,
    string? VehicleType) : ICommand;
