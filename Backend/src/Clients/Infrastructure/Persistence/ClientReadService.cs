using Microsoft.EntityFrameworkCore;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Application.Clients;
using NorthernLink.Clients.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Read side — queries clients.rm_clients and maps to the public contract.</summary>
internal sealed class ClientReadService(ClientsDbContext context) : IClientReadService
{
    public async Task<IReadOnlyList<ClientResponse>> GetClientsAsync(CancellationToken cancellationToken = default)
    {
        var clients = await context.ClientReadModels
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return clients.Select(ToResponse).ToList();
    }

    public async Task<ClientResponse?> GetClientAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var client = await context.ClientReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);

        return client is null ? null : ToResponse(client);
    }

    private static ClientResponse ToResponse(ClientReadModel client) => new(
        client.Id,
        client.Name,
        client.Type,
        client.ServiceType,
        client.Tag,
        client.AgreementReference,
        client.Notes,
        client.CreatedAtUtc,
        client.UpdatedAtUtc,
        client.ActiveContractId is { } contractId
            ? new ActiveContractSummary(
                contractId,
                client.ActiveContractStartDate!.Value,
                client.ActiveContractEndDate,
                client.ActiveContractBillingModel!,
                client.ActiveContractRatePerRoundTripCad,
                client.ActiveContractGstApplicable!.Value,
                client.ActiveContractBudgetCode,
                client.ActiveContractBillingFrequency!,
                client.ActiveContractNetTermsDays!.Value,
                client.ActiveContractDefaultPoNumber)
            : null);
}
