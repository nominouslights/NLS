using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Documents;

namespace NorthernLink.Fleet.Application.Documents.GetAll;

public sealed class GetAllDocumentsQueryHandler(IVehicleDocumentReadService readService)
    : IQueryHandler<GetAllDocumentsQuery, IReadOnlyList<VehicleDocumentResponse>>
{
    public async Task<Result<IReadOnlyList<VehicleDocumentResponse>>> Handle(
        GetAllDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var documents = await readService.GetAllAsync(cancellationToken);
        return Result.Success(documents);
    }
}
