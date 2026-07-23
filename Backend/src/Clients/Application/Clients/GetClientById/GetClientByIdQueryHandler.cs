using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.Clients;

namespace NorthernLink.Clients.Application.Clients.GetClientById;

public sealed class GetClientByIdQueryHandler(IClientReadService readService)
    : IQueryHandler<GetClientByIdQuery, ClientResponse>
{
    public async Task<Result<ClientResponse>> Handle(
        GetClientByIdQuery query,
        CancellationToken cancellationToken)
    {
        var client = await readService.GetClientAsync(query.ClientId, cancellationToken);

        return client is null
            ? Result.Failure<ClientResponse>(ClientErrors.NotFound)
            : Result.Success(client);
    }
}
