namespace NorthernLink.Trips.Application.Stops;

/// <summary>
/// Public contract for a catalog stop (the Stops screen and the route-builder picker).
/// <see cref="StopType"/> travels as a string name ("Hub", …) or null. The address is
/// flattened into individual fields; coordinates are plain numbers.
/// </summary>
public sealed record StopResponse(
    Guid Id,
    string Name,
    string? StopType,
    string? Street,
    string City,
    string Province,
    string? PostalCode,
    string Country,
    double Latitude,
    double Longitude,
    string? Notes,
    bool Active,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
