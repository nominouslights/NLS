using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Commands.Drivers;

public sealed record SetDriverStatusCommand(Guid Id, DriverStatus Status) : ICommand;
