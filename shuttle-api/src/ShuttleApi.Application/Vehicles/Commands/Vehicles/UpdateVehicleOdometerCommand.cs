using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Commands.Vehicles;

public sealed record UpdateVehicleOdometerCommand(Guid Id, int NewOdometerKm) : ICommand;
