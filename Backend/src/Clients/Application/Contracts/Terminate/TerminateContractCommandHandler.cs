using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.Contracts;

namespace NorthernLink.Clients.Application.Contracts.Terminate;

public sealed class TerminateContractCommandHandler(IContractRepository repository)
    : ICommandHandler<TerminateContractCommand>
{
    public async Task<Result> Handle(TerminateContractCommand command, CancellationToken cancellationToken)
    {
        var contract = await repository.GetByIdAsync(command.ContractId, cancellationToken);
        if (contract is null)
        {
            return Result.Failure(ContractErrors.NotFound);
        }

        var result = contract.Terminate();
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
