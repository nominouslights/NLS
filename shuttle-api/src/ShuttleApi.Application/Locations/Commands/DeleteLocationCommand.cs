using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Locations;

public sealed record DeleteLocationCommand(Guid Id) : ICommand;
