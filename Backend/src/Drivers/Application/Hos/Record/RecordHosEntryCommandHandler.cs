using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Record;

public sealed class RecordHosEntryCommandHandler(IHosLogRepository repository)
    : ICommandHandler<RecordHosEntryCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RecordHosEntryCommand command, CancellationToken cancellationToken)
    {
        if (!await repository.DriverExistsAsync(command.DriverId, cancellationToken))
        {
            return Result.Failure<Guid>(DriverErrors.NotFound);
        }

        var entryResult = HosLogEntry.RecordManualEntry(
            command.TenantId,
            command.DriverId,
            command.Date,
            command.Duty,
            command.OnDutyHours,
            command.DrivingHours,
            command.OffDutyHours,
            command.EnteredBy,
            command.Note);

        if (entryResult.IsFailure)
        {
            return Result.Failure<Guid>(entryResult.Error);
        }

        repository.Add(entryResult.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(entryResult.Value.Id);
    }
}
