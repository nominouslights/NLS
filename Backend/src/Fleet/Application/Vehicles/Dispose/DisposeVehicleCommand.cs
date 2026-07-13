using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Application.Vehicles.Dispose;

/// <summary>
/// Terminal disposal of a retired vehicle (Sold or Recycled). Separate from the generic
/// status change because it captures a sale price and stamps the disposal timestamp.
/// </summary>
public sealed record DisposeVehicleCommand(
    Guid TenantId,
    Guid VehicleId,
    DisposalMethod Method,
    decimal? SalePriceCad,
    string? Note) : ICommand;
