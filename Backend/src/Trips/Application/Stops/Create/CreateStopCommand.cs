using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Stops;

namespace NorthernLink.Trips.Application.Stops.Create;

/// <summary>Creates a catalog stop (active by default). Coordinates come from the frontend's Places selection.</summary>
public sealed record CreateStopCommand(
    Guid TenantId,
    string Name,
    StopType? Type,
    string? Street,
    string City,
    string Province,
    string? PostalCode,
    string Country,
    double Latitude,
    double Longitude,
    string? Notes) : ICommand<Guid>;
