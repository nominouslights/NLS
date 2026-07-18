using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Documents;

namespace NorthernLink.Fleet.Application.Documents.GetForVehicle;

public sealed class GetVehicleDocumentsQueryHandler(IVehicleDocumentReadService readService)
    : IQueryHandler<GetVehicleDocumentsQuery, IReadOnlyList<VehicleDocumentResponse>>
{
    public async Task<Result<IReadOnlyList<VehicleDocumentResponse>>> Handle(
        GetVehicleDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var documents = await readService.GetForVehicleAsync(query.VehicleId, cancellationToken);
        return Result.Success(documents);
    }
}
