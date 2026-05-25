using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Clients;

internal sealed class GetClientByIdQueryHandler(
    IClientRepository clientRepository,
    IContractRepository contractRepository)
    : IRequestHandler<GetClientByIdQuery, ClientDetailResult>
{
    public async Task<ClientDetailResult> Handle(GetClientByIdQuery request, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Client {request.Id} not found.");

        var activeContract = await contractRepository.GetActiveByClientIdAsync(client.Id, cancellationToken);

        ContractSummaryResult? contractSummary = activeContract is null ? null : new ContractSummaryResult(
            activeContract.Id,
            activeContract.StartDate,
            activeContract.RenewalDate,
            activeContract.IsExpiringSoon,
            activeContract.Notes,
            activeContract.RateLines.Select(r => new RateLineResult(
                r.Id,
                r.BillingCode,
                r.Description,
                r.VehicleType,
                r.MaxDistanceKm,
                r.CargoIncluded,
                r.DayRate)).ToList());

        return new ClientDetailResult(
            client.Id,
            client.BusinessName,
            client.ServiceType.ToString(),
            client.PrimaryContactName,
            client.PrimaryContactTitle,
            client.Phone,
            client.Email,
            client.StreetAddress,
            client.City,
            client.Province,
            client.PostalCode,
            client.GstHstNumber,
            client.PreferredPaymentMethod,
            client.NetPaymentTerms,
            client.OutstandingBalance,
            client.ComplianceNotes,
            client.IsMinesite,
            client.IsActive,
            client.CreatedAt,
            contractSummary,
            client.Industry,
            client.ProjectSite);
    }
}
