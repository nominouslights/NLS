using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Locations;

public sealed record RestoreLocationCommand(Guid LocationId) : ICommand;
