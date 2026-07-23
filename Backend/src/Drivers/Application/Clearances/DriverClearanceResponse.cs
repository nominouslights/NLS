namespace NorthernLink.Drivers.Application.Clearances;

/// <summary>
/// The Drivers module's public representation of a client-site clearance. The expiry
/// status chip is derived by the frontend from <paramref name="Expiry"/>.
/// </summary>
public sealed record DriverClearanceResponse(
    Guid Id,
    Guid DriverId,
    string Title,
    string ClientName,
    DateOnly? Expiry,
    DateTimeOffset GrantedAtUtc);
