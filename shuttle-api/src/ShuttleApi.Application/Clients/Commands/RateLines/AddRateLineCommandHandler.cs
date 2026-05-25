using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Clients;

internal sealed class AddRateLineCommandHandler(IContractRepository contractRepository)
    : IRequestHandler<AddRateLineCommand, AddRateLineResult>
{
    public async Task<AddRateLineResult> Handle(AddRateLineCommand request, CancellationToken cancellationToken)
    {
        var contractExists = await contractRepository.GetByIdAsync(request.ContractId, cancellationToken)
            is not null;
        if (!contractExists)
            throw new NotFoundException($"Contract {request.ContractId} not found.");

        var rateLine = ContractRateLine.Create(
            request.ContractId,
            request.BillingCode,
            request.Description,
            request.VehicleType,
            request.MaxDistanceKm,
            request.CargoIncluded,
            request.DayRate);

        await contractRepository.AddRateLineAsync(rateLine, cancellationToken);

        return new AddRateLineResult(rateLine.Id);
    }
}
