using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Queries.OdometerHistory;

public sealed record GetVehicleOdometerHistoryQuery(Guid VehicleId)
    : IQuery<IReadOnlyList<OdometerHistoryEntryResult>>;

public sealed record OdometerHistoryEntryResult(
    DateTime Date,
    int OdometerKm,
    string Source,       // "Trip" | "Service" | "Fuel"
    Guid ReferenceId,
    string? Notes);
