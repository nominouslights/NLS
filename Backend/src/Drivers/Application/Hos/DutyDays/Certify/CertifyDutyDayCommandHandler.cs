using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.DutyDays.Certify;

public sealed class CertifyDutyDayCommandHandler(
    IDutyDayRepository repository,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<CertifyDutyDayCommand>
{
    public async Task<Result> Handle(CertifyDutyDayCommand command, CancellationToken cancellationToken)
    {
        if (actor.UserId is not { } userId)
        {
            return Result.Failure(HosErrors.CertifyDriverOnly);
        }

        var day = await repository.GetAsync(command.DriverId, command.LocalDate, cancellationToken);
        if (day is null)
        {
            return Result.Failure(HosErrors.DayNotFound);
        }

        var result = day.Certify(userId, clock.GetUtcNow(), DutyLedgerAssembler.Calendar);
        if (result.IsFailure)
        {
            return result;
        }

        // An already-certified day raises nothing and is left Unchanged, so this save is a no-op.
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
