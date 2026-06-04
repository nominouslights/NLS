using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Queries.Vehicles;

internal sealed class GetArchivedVehiclesQueryHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<GetArchivedVehiclesQuery, IReadOnlyList<ArchivedVehicleResult>>
{
    public async Task<IReadOnlyList<ArchivedVehicleResult>> Handle(
        GetArchivedVehiclesQuery request,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddYears(-1);
        await vehicleRepository.PurgeExpiredAsync(cutoff, cancellationToken);

        var archived = await vehicleRepository.GetAllArchivedAsync(cancellationToken);

        return archived.Select(v => new ArchivedVehicleResult(
            v.Id,
            v.UnitCode,
            v.Make,
            v.Model,
            v.Year,
            v.LicensePlate,
            v.VehicleType.ToString(),
            v.Status.ToString(),
            v.StatusNote,
            v.PassengerCapacity,
            v.CurrentOdometerKm,
            v.IsActive,
            v.DeletedAt
        )).ToList();
    }
}
