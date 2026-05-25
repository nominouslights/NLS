using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Drivers.Commands.Drivers;

public sealed record DeleteDriverCommand(Guid Id) : ICommand;
