using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Inspections;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Application.Inspections.GetInspections;

public sealed class GetVehicleInspectionsQueryHandler(IVehicleInspectionReadService readService)
    : IQueryHandler<GetVehicleInspectionsQuery, IReadOnlyList<VehicleInspectionResponse>>
{
    public async Task<Result<IReadOnlyList<VehicleInspectionResponse>>> Handle(
        GetVehicleInspectionsQuery query,
        CancellationToken cancellationToken)
    {
        var inspections = await readService.GetInspectionsAsync(query.Unit, cancellationToken);
        return Result.Success(inspections);
    }
}
