using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Clients;

internal sealed class CreateContractCommandHandler(
    IClientRepository clientRepository,
    IContractRepository contractRepository)
    : IRequestHandler<CreateContractCommand, CreateContractResult>
{
    public async Task<CreateContractResult> Handle(CreateContractCommand request, CancellationToken cancellationToken)
    {
        var clientExists = await clientRepository.GetByIdAsync(request.ClientId, cancellationToken)
            is not null;
        if (!clientExists)
            throw new NotFoundException($"Client {request.ClientId} not found.");

        var existing = await contractRepository.GetActiveByClientIdAsync(request.ClientId, cancellationToken);
        if (existing is not null)
        {
            existing.Deactivate();
            await contractRepository.UpdateAsync(existing, cancellationToken);
        }

        var contract = Contract.Create(
            request.ClientId,
            DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
            DateTime.SpecifyKind(request.RenewalDate, DateTimeKind.Utc),
            request.Notes);
        await contractRepository.AddAsync(contract, cancellationToken);

        foreach (var dto in request.RateLines)
        {
            var rateLine = ContractRateLine.Create(
                contract.Id,
                dto.BillingCode,
                dto.Description,
                dto.VehicleType,
                dto.MaxDistanceKm,
                dto.CargoIncluded,
                dto.DayRate);
            await contractRepository.AddRateLineAsync(rateLine, cancellationToken);
        }

        return new CreateContractResult(contract.Id);
    }
}
