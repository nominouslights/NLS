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
        var results = new List<ClientListItemResult>(clients.Count);

        foreach (var client in clients)
        {
            var activeContract = await contractRepository.GetActiveByClientIdAsync(client.Id, cancellationToken);
            results.Add(new ClientListItemResult(
                client.Id,
                client.BusinessName,
                client.ServiceType.ToString(),
                client.PrimaryContactName,
                client.Phone,
                client.Email,
                client.IsActive,
                activeContract?.EndDate,
                activeContract?.IsExpiringSoon ?? false));
        }

        return results;
    }
}
