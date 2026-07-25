using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.GetForDriver;

public sealed class GetHosEntriesQueryHandler(IHosLogReadService readService)
    : IQueryHandler<GetHosEntriesQuery, IReadOnlyList<HosEntryResponse>>
{
    public async Task<Result<IReadOnlyList<HosEntryResponse>>> Handle(
        GetHosEntriesQuery query,
        CancellationToken cancellationToken)
    {
        var entries = await readService.GetForDriverAsync(query.DriverId, cancellationToken);
        return Result.Success(entries);
    }
}
