namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Issues the next per-tenant shipment number ("SH-####"). Server-issued for the same reason
/// trip numbers are: a client-supplied identifier is a collision waiting to happen, and cargo
/// is registered from the Driver Field App as well as the console.
/// </summary>
public interface IShipmentNumberGenerator
{
    Task<string> NextAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
