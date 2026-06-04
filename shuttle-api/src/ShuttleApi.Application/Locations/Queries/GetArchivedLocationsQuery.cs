using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Locations;

public sealed record GetArchivedLocationsQuery : IQuery<IReadOnlyList<ArchivedLocationResult>>;

public sealed record ArchivedLocationResult(
    Guid Id,
    string Name,
    string? Address,
    double? Latitude,
    double? Longitude,
    DateTime CreatedAt,
    DateTime? DeletedAt);
