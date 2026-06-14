using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Application.Clients;

internal sealed class GetClientsQueryHandler(
    IClientRepository clientRepository,
    IContractRepository contractRepository)
    : IRequestHandler<GetClientsQuery, IReadOnlyList<ClientListItemResult>>
{
    public async Task<IReadOnlyList<ClientListItemResult>> Handle(GetClientsQuery request, CancellationToken cancellationToken)
    {
        var clients = await clientRepository.GetAllAsync(cancellationToken);
        var contractMap = await contractRepository.GetActiveBatchByClientIdsAsync(
            clients.Select(c => c.Id), cancellationToken);

        return clients.Select(client =>
        {
            contractMap.TryGetValue(client.Id, out var contract);
            return new ClientListItemResult(
                client.Id,
                client.BusinessName,
                client.ServiceType.ToString(),
                client.PrimaryContactName,
                client.Phone,
                client.Email,
                client.IsActive,
                contract?.EndDate,
                contract?.IsExpiringSoon ?? false);
        }).ToList();
    }
}
