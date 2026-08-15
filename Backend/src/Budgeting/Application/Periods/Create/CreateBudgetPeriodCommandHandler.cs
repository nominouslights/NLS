using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Periods.Create;

/// <summary>
/// Creates a budget period after enforcing that the tenant's periods never share a day:
/// ranges are StartsOn..EndsOn <b>inclusive</b>, and any intersection with an existing
/// period — including a month inside an existing quarter — is an overlap (the
/// <c>CreateContractCommandHandler</c> precedent). The unique (tenant, granularity, year,
/// ordinal) index is the DB backstop for the double-click race only; this check is the
/// real rule, since it also catches cross-granularity collisions the index cannot.
/// </summary>
public sealed class CreateBudgetPeriodCommandHandler(IBudgetPeriodRepository repository)
    : ICommandHandler<CreateBudgetPeriodCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateBudgetPeriodCommand command, CancellationToken cancellationToken)
    {
        var periodResult = BudgetPeriod.Create(
            command.TenantId, command.Granularity, command.Year, command.Ordinal);

        if (periodResult.IsFailure)
        {
            return Result.Failure<Guid>(periodResult.Error);
        }

        var period = periodResult.Value;

        var existing = await repository.GetAllAsync(cancellationToken);
        if (existing.Any(other => other.Overlaps(period.StartsOn, period.EndsOn)))
        {
            return Result.Failure<Guid>(BudgetPeriodErrors.Overlapping);
        }

        repository.Add(period);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(period.Id);
    }
}
