using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.DutyEvents.Record;

public sealed class RecordDutyEventCommandHandler(
    IDutyDayRepository repository,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<RecordDutyEventCommand, DutyEventRecordedResponse>
{
    public async Task<Result<DutyEventRecordedResponse>> Handle(RecordDutyEventCommand command, CancellationToken cancellationToken)
    {
        if (!await repository.DriverExistsAsync(command.DriverId, cancellationToken))
        {
            return Result.Failure<DutyEventRecordedResponse>(DriverErrors.NotFound);
        }

        var now = clock.GetUtcNow();
        var localDate = DutyLedgerAssembler.Calendar.DayOf(command.AtUtc.ToUnixTimeSeconds());

        var day = await repository.GetAsync(command.DriverId, localDate, cancellationToken);
        var opened = false;
        if (day is null)
        {
            day = DutyDay.Open(command.TenantId, command.DriverId, localDate, now);
            opened = true;
        }

        var draft = new DutyEventDraft(
            command.EventId ?? Guid.NewGuid(),
            command.AtUtc,
            command.Status,
            command.Source,
            actor.UserId,
            command.EnteredBy,
            command.CarrierName,
            command.Note,
            command.OdometerStartKm,
            command.OdometerEndKm,
            command.PcNoPassengersOrCargo,
            command.PcNoTrailer,
            command.PcNoWork);

        var result = day.Append(draft, now, DutyLedgerAssembler.Calendar);
        if (result.IsFailure)
        {
            return Result.Failure<DutyEventRecordedResponse>(result.Error);
        }

        if (result.Value == DutyAppendOutcome.AlreadyRecorded)
        {
            // Replay: nothing changed, nothing to save, same answer as the first time.
            return Result.Success(new DutyEventRecordedResponse(draft.EventId, day.Id, localDate, Created: false));
        }

        if (opened)
        {
            repository.Add(day);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(new DutyEventRecordedResponse(draft.EventId, day.Id, localDate, Created: true));
    }
}
