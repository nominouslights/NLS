using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;

namespace NorthernLink.Clients.Application.Clients.GetClients;

public sealed class GetClientsQueryHandler(IClientReadService readService)
    : IQueryHandler<GetClientsQuery, IReadOnlyList<ClientResponse>>
{
    public async Task<Result<IReadOnlyList<ClientResponse>>> Handle(
        GetClientsQuery query,
        CancellationToken cancellationToken)
    {
        var clients = await readService.GetClientsAsync(cancellationToken);
        return Result.Success(clients);
    }
}
