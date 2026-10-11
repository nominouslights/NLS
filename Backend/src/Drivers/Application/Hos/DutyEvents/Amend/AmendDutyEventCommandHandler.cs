using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.DutyEvents.Amend;

public sealed class AmendDutyEventCommandHandler(
    IDutyDayRepository repository,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<AmendDutyEventCommand, DutyEventAmendedResponse>
{
    public async Task<Result<DutyEventAmendedResponse>> Handle(AmendDutyEventCommand command, CancellationToken cancellationToken)
    {
        if (!await repository.DriverExistsAsync(command.DriverId, cancellationToken))
        {
            return Result.Failure<DutyEventAmendedResponse>(DriverErrors.NotFound);
        }

        var localDate = DutyLedgerAssembler.Calendar.DayOf(command.AtUtc.ToUnixTimeSeconds());
        var day = await repository.GetAsync(command.DriverId, localDate, cancellationToken);
        if (day is null)
        {
            return Result.Failure<DutyEventAmendedResponse>(HosErrors.EventNotFound);
        }

        var now = clock.GetUtcNow();
        var replacement = new DutyEventDraft(
            command.ReplacementEventId ?? Guid.NewGuid(),
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

        var result = day.Amend(command.OriginalEventId, replacement, command.Reason, now, DutyLedgerAssembler.Calendar);
        if (result.IsFailure)
        {
            return Result.Failure<DutyEventAmendedResponse>(result.Error);
        }

        var created = result.Value == DutyAppendOutcome.Recorded;
        if (created)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new DutyEventAmendedResponse(
            command.OriginalEventId,
            replacement.EventId,
            day.Id,
            localDate,
            day.RecertificationRequired,
            created));
    }
}
