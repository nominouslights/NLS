using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Commands.FuelEntries;

public sealed record AddFuelEntryCommand(
    Guid VehicleId,
    DateTime FuelledAt,
    decimal FuelLitres,
    decimal TotalCostDollars,
    int? OdometerAtFuelling,
    string? Notes,
    byte[]? ReceiptPhotoBytes = null,
    string? ReceiptPhotoFileName = null,
    string? ReceiptPhotoContentType = null) : ICommand<AddFuelEntryResult>;

public sealed record AddFuelEntryResult(Guid EntryId);
