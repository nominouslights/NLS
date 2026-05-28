using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Locations;

public sealed record GetLocationsQuery : IQuery<IReadOnlyList<LocationListItemResult>>;

public sealed record LocationListItemResult(
    Guid Id,
    string Name,
    string? Address,
    double? Latitude,
    double? Longitude,
    DateTime CreatedAt);
