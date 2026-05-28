using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Locations;

public sealed record UpdateLocationCommand(
    Guid Id,
    string Name,
    string? Address,
    double? Latitude,
    double? Longitude) : ICommand;
