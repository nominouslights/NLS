using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Clients;

internal sealed class UpdateContractCommandHandler(IContractRepository contractRepository)
    : IRequestHandler<UpdateContractCommand>
{
    public async Task Handle(UpdateContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await contractRepository.GetByIdAsync(request.ContractId, cancellationToken)
            ?? throw new NotFoundException($"Contract {request.ContractId} not found.");

        contract.Update(request.StartDate, request.RenewalDate, request.Notes);
        await contractRepository.UpdateAsync(contract, cancellationToken);
    }
}
