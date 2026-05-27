using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Commands.Vehicles;

public sealed record SetVehicleOutOfServiceCommand(Guid Id, string Reason) : ICommand;
