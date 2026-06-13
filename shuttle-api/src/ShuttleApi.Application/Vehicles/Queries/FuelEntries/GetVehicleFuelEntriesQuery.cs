using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Queries.FuelEntries;

public sealed record GetVehicleFuelEntriesQuery(Guid VehicleId) : IQuery<IReadOnlyList<FuelEntryResult>>;

public sealed record FuelEntryResult(
    Guid Id,
    DateTime FuelledAt,
    decimal FuelLitres,
    decimal TotalCostDollars,
    int? OdometerAtFuelling,
    string? ReceiptPhotoUrl,
    string? Notes,
    DateTime CreatedAt);
