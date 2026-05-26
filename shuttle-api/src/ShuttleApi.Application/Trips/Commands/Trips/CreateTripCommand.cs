using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record CreateTripCommand(
    Guid ClientId,
    string? PurchaseOrderNumber,
    string? VehicleType,
    DateTime ScheduledAt,
    string? Notes,
    IReadOnlyList<StopDto> Stops) : ICommand<CreateTripResult>;

public sealed record CreateTripResult(Guid Id);
