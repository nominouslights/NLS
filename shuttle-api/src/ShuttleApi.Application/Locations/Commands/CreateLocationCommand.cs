using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Locations;

public sealed record CreateLocationCommand(
    string Name,
    string? Address,
    double? Latitude,
    double? Longitude) : ICommand<CreateLocationResult>;

public sealed record CreateLocationResult(Guid Id);
