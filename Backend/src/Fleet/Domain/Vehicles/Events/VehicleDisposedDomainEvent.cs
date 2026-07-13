using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Vehicles.Events;

/// <summary>Raised when a retired vehicle is sold or recycled out of the fleet.</summary>
public sealed record VehicleDisposedDomainEvent(
    Guid VehicleId,
    DisposalMethod Method,
    decimal? SalePriceCad) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
