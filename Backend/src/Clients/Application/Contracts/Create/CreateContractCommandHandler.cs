using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.Contracts;

namespace NorthernLink.Clients.Application.Contracts.Create;

/// <summary>
/// Creates a contract after enforcing the one-active-contract-per-client rule: periods are
/// StartDate..EndDate <b>inclusive</b>, a null EndDate is open-ended (evergreen), and any
/// intersection with another <b>active</b> contract — including a same-day boundary — is an
/// overlap. Ended and terminated contracts never block. The client's name is snapshotted
/// onto the contract here so the full-snapshot integration event never needs a join.
/// </summary>
public sealed class CreateContractCommandHandler(
    IClientRepository clientRepository,
    IContractRepository contractRepository)
    : ICommandHandler<CreateContractCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateContractCommand command, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByIdAsync(command.ClientId, cancellationToken);
        if (client is null)
        {
            return Result.Failure<Guid>(ClientErrors.NotFound);
        }

        var existing = await contractRepository.GetByClientIdAsync(command.ClientId, cancellationToken);
        if (existing.Any(contract =>
                contract.Status == ContractStatus.Active
                && contract.Overlaps(command.StartDate, command.EndDate)))
        {
            return Result.Failure<Guid>(ContractErrors.OverlappingPeriod);
        }

        var contractResult = Contract.Create(
            command.TenantId,
            command.ClientId,
            client.Name,
            command.StartDate,
            command.EndDate,
            command.BillingModel,
            command.RatePerRoundTripCad,
            command.GstApplicable,
            command.BudgetCode,
            command.BillingFrequency,
            command.NetTermsDays,
            command.DefaultPoNumber);

        if (contractResult.IsFailure)
        {
            return Result.Failure<Guid>(contractResult.Error);
        }

        contractRepository.Add(contractResult.Value);
        await contractRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(contractResult.Value.Id);
    }
}
