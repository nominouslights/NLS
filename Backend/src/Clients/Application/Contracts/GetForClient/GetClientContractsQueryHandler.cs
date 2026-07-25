using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;

namespace NorthernLink.Clients.Application.Contracts.GetForClient;

public sealed class GetClientContractsQueryHandler(IContractReadService readService)
    : IQueryHandler<GetClientContractsQuery, IReadOnlyList<ContractResponse>>
{
    public async Task<Result<IReadOnlyList<ContractResponse>>> Handle(
        GetClientContractsQuery query,
        CancellationToken cancellationToken)
    {
        var contracts = await readService.GetForClientAsync(query.ClientId, cancellationToken);
        return Result.Success(contracts);
    }
}
