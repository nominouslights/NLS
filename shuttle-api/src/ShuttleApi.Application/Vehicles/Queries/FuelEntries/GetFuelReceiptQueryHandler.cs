using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Queries.FuelEntries;

internal sealed class GetFuelReceiptQueryHandler(
    IVehicleRepository vehicleRepository,
    IFileStorageService fileStorage)
    : IRequestHandler<GetFuelReceiptQuery, FuelReceiptResult?>
{
    public async Task<FuelReceiptResult?> Handle(
        GetFuelReceiptQuery request,
        CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdWithRecordsAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.VehicleId} not found.");

        var entry = vehicle.FuelEntries.FirstOrDefault(e => e.Id == request.EntryId);
        if (entry?.ReceiptPhotoUrl is null) return null;

        var fileResult = await fileStorage.RetrieveAsync(entry.ReceiptPhotoUrl, cancellationToken);
        return new FuelReceiptResult(
            fileResult.Data,
            fileResult.ContentType,
            $"fuel-receipt-{request.EntryId}.jpg");
    }
}
