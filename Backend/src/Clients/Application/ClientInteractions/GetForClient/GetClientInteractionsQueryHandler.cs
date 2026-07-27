using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;

namespace NorthernLink.Clients.Application.ClientInteractions.GetForClient;

public sealed class GetClientInteractionsQueryHandler(IClientInteractionReadService readService)
    : IQueryHandler<GetClientInteractionsQuery, IReadOnlyList<ClientInteractionResponse>>
{
    public async Task<Result<IReadOnlyList<ClientInteractionResponse>>> Handle(
        GetClientInteractionsQuery query,
        CancellationToken cancellationToken)
    {
        var interactions = await readService.GetForClientAsync(query.TenantId, query.ClientId, cancellationToken);
        return Result.Success(interactions);
    }
}
