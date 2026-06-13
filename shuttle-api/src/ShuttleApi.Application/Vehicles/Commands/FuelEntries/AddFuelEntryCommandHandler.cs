using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.FuelEntries;

internal sealed class AddFuelEntryCommandHandler(
    IVehicleRepository vehicleRepository,
    IFileStorageService fileStorage)
    : IRequestHandler<AddFuelEntryCommand, AddFuelEntryResult>
{
    public async Task<AddFuelEntryResult> Handle(AddFuelEntryCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.VehicleId} not found.");

        string? receiptStorageKey = null;
        if (request.ReceiptPhotoBytes is { Length: > 0 } bytes)
        {
            var fileName = request.ReceiptPhotoFileName ?? $"fuel-receipt-{Guid.NewGuid()}.jpg";
            var contentType = request.ReceiptPhotoContentType ?? "image/jpeg";
            receiptStorageKey = await fileStorage.StoreAsync(fileName, contentType, bytes, cancellationToken);
        }

        var entry = VehicleFuelEntry.Create(
            request.VehicleId,
            DateTime.SpecifyKind(request.FuelledAt, DateTimeKind.Utc),
            request.FuelLitres,
            request.TotalCostDollars,
            request.OdometerAtFuelling,
            receiptStorageKey,
            request.Notes);

        vehicle.AddFuelEntry(entry);

        await vehicleRepository.UpdateAsync(vehicle, cancellationToken);

        return new AddFuelEntryResult(entry.Id);
    }
}
