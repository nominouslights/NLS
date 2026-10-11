using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.DutyDays.Transcribe;

public sealed class TranscribeDutyDayCommandHandler(
    IDutyDayRepository repository,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<TranscribeDutyDayCommand, TranscriptionRecordedResponse>
{
    public async Task<Result<TranscriptionRecordedResponse>> Handle(TranscribeDutyDayCommand command, CancellationToken cancellationToken)
    {
        if (command.Source is not (DutyEventSource.EldTranscription or DutyEventSource.PaperLog))
        {
            return Result.Failure<TranscriptionRecordedResponse>(HosErrors.TranscriptionSourceInvalid);
        }

        if (string.IsNullOrWhiteSpace(command.EnteredBy))
        {
            return Result.Failure<TranscriptionRecordedResponse>(HosErrors.EnteredByRequired);
        }

        if (command.Entries.Count == 0)
        {
            return Result.Failure<TranscriptionRecordedResponse>(HosErrors.TranscriptionEmpty);
        }

        for (var i = 1; i < command.Entries.Count; i++)
        {
            if (command.Entries[i].Time <= command.Entries[i - 1].Time)
            {
                return Result.Failure<TranscriptionRecordedResponse>(HosErrors.TranscriptionNotChronological);
            }
        }

        if (!await repository.DriverExistsAsync(command.DriverId, cancellationToken))
        {
            return Result.Failure<TranscriptionRecordedResponse>(DriverErrors.NotFound);
        }

        var now = clock.GetUtcNow();
        var day = await repository.GetAsync(command.DriverId, command.LocalDate, cancellationToken);
        var opened = false;
        if (day is null)
        {
            day = DutyDay.Open(command.TenantId, command.DriverId, command.LocalDate, now);
            opened = true;
        }

        var appended = 0;
        var replayed = 0;
        for (var i = 0; i < command.Entries.Count; i++)
        {
            var entry = command.Entries[i];
            var draft = new DutyEventDraft(
                TranscriptionEventIds.For(command.TranscriptionId, i),
                HosLocalTime.ToUtc(command.LocalDate, entry.Time),
                entry.Status,
                command.Source,
                actor.UserId,
                command.EnteredBy,
                command.CarrierName,
                entry.Note);

            // All-or-nothing: a failure here returns before any save, and the scoped context is
            // discarded with the half-built day still only in memory.
            var result = day.Append(draft, now, DutyLedgerAssembler.Calendar);
            if (result.IsFailure)
            {
                return Result.Failure<TranscriptionRecordedResponse>(result.Error);
            }

            if (result.Value == DutyAppendOutcome.Recorded)
            {
                appended++;
            }
            else
            {
                replayed++;
            }
        }

        if (appended > 0)
        {
            if (opened)
            {
                repository.Add(day);
            }

            await repository.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new TranscriptionRecordedResponse(day.Id, command.LocalDate, appended, replayed));
    }
}
