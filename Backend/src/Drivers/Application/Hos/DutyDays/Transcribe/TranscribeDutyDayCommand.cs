using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.DutyDays.Transcribe;

/// <summary>
/// Dispatch keys a whole day from the ELD display or a paper log. All-or-nothing: every entry
/// is validated against the ledger before anything is saved, and a failure leaves the day
/// untouched. Each entry's id is derived from <paramref name="TranscriptionId"/> and its index
/// (<see cref="TranscriptionEventIds"/>), so a retried request appends nothing new and reports
/// what was already there. <paramref name="Source"/> must be ELD transcription or paper log;
/// <paramref name="EnteredBy"/> is the dispatcher's name and is required.
/// </summary>
public sealed record TranscribeDutyDayCommand(
    Guid TenantId,
    Guid DriverId,
    DateOnly LocalDate,
    Guid TranscriptionId,
    DutyEventSource Source,
    string? EnteredBy,
    string? CarrierName,
    IReadOnlyList<TranscriptionEntry> Entries) : ICommand<TranscriptionRecordedResponse>;

/// <summary>One line of a transcription: local wall-clock time on the day and the status it began.</summary>
public sealed record TranscriptionEntry(TimeOnly Time, DutyStatus Status, string? Note = null);
