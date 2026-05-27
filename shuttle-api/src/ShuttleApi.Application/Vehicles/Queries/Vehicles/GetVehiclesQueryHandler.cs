using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Queries.Vehicles;

internal sealed class GetVehiclesQueryHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<GetVehiclesQuery, IReadOnlyList<VehicleListItemResult>>
{
    public async Task<IReadOnlyList<VehicleListItemResult>> Handle(
        GetVehiclesQuery request,
        CancellationToken cancellationToken)
    {
        var vehicles = await vehicleRepository.GetAllAsync(cancellationToken);

        return vehicles.Select(v => new VehicleListItemResult(
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
            v.IsRegistrationExpiringSoon,
            v.IsInsuranceExpiringSoon,
            VehicleReadiness.ComputeScore(v),
            VehicleReadiness.GetAlerts(v),
            v.IsActive
        )).ToList();
    }
}
