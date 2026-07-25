using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.Contracts;

namespace NorthernLink.Clients.Application.Contracts.Update;

/// <summary>
/// Amends an active contract. Re-runs the overlap check against the client's <b>other</b>
/// active contracts (the contract being amended is excluded — its own current period may
/// of course intersect its new one).
/// </summary>
public sealed class UpdateContractCommandHandler(IContractRepository repository)
    : ICommandHandler<UpdateContractCommand>
{
    public async Task<Result> Handle(UpdateContractCommand command, CancellationToken cancellationToken)
    {
        var contract = await repository.GetByIdAsync(command.ContractId, cancellationToken);
        if (contract is null)
        {
            return Result.Failure(ContractErrors.NotFound);
        }

        var siblings = await repository.GetByClientIdAsync(contract.ClientId, cancellationToken);
        if (siblings.Any(other =>
                other.Id != contract.Id
                && other.Status == ContractStatus.Active
                && other.Overlaps(command.StartDate, command.EndDate)))
        {
            return Result.Failure(ContractErrors.OverlappingPeriod);
        }

        var result = contract.Update(
            command.StartDate,
            command.EndDate,
            command.BillingModel,
            command.RatePerRoundTripCad,
            command.GstApplicable,
            command.BudgetCode,
            command.BillingFrequency,
            command.NetTermsDays,
            command.DefaultPoNumber);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
