using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Commands.Vehicles;

public sealed record DeleteVehicleCommand(Guid Id) : ICommand;
