using NorthernLink.Drivers.Domain.Hos.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// One day's Hours-of-Service duty log for a driver — its own aggregate (child of Driver,
/// keyed on <see cref="DriverId"/>), added whole and never mutated. Two factories, mirroring
/// Fleet's <c>VehicleInspection</c>: <see cref="RecordManualEntry"/> (a dispatcher paper
/// backup, source <see cref="HosLogEntrySource.ManualPaperBackup"/>, requires a dispatcher
/// name) and <see cref="RecordFromDriverApp"/> (the primary field source, source
/// <see cref="HosLogEntrySource.DriverApp"/>, no dispatcher name).
///
/// Invariants are deliberately light: a valid duty status and each hours value in [0, 24].
/// A CVDHS-violating day (e.g. driving &gt; 13h) is NOT rejected — logging over-limit days
/// is the whole point of compliance tracking; the violation rule engine runs client-side
/// over the raw entries. Both factories raise <see cref="HosDutyEntryRecordedDomainEvent"/>
/// so the read side projects incrementally (see the event's remarks).
/// </summary>
public sealed class HosLogEntry : AggregateRoot, ITenantScoped
{
    private const decimal MinHours = 0m;
    private const decimal MaxHours = 24m;

    private HosLogEntry()
    {
        // EF Core materialization only.
    }

    public Guid TenantId { get; private set; }
    public Guid DriverId { get; private set; }
    public DateOnly Date { get; private set; }
    public DutyStatus Duty { get; private set; }
    public decimal OnDutyHours { get; private set; }
    public decimal DrivingHours { get; private set; }
    public decimal OffDutyHours { get; private set; }
    public HosLogEntrySource Source { get; private set; }

    /// <summary>Dispatcher name for a manual paper-backup entry; null for a driver-app entry.</summary>
    public string? EnteredBy { get; private set; }

    public string? Note { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }

    /// <summary>
    /// A dispatcher paper-backup entry, typed into the console when the Driver App was
    /// unavailable. <paramref name="enteredBy"/> (the dispatcher's name) is required.
    /// </summary>
    public static Result<HosLogEntry> RecordManualEntry(
        Guid tenantId,
        Guid driverId,
        DateOnly date,
        DutyStatus duty,
        decimal onDutyHours,
        decimal drivingHours,
        decimal offDutyHours,
        string? enteredBy,
        string? note)
    {
        if (string.IsNullOrWhiteSpace(enteredBy))
        {
            return Result.Failure<HosLogEntry>(HosErrors.EnteredByRequired);
        }

        return Record(
            tenantId, driverId, date, duty, onDutyHours, drivingHours, offDutyHours,
            HosLogEntrySource.ManualPaperBackup, enteredBy.Trim(), note);
    }

    /// <summary>
    /// A duty log submitted from the Driver Field App — the primary source. No dispatcher
    /// name (<see cref="EnteredBy"/> is null). The ingestion mechanism (an integration-event
    /// consumer) is not wired yet; this factory keeps the model complete.
    /// </summary>
    public static Result<HosLogEntry> RecordFromDriverApp(
        Guid tenantId,
        Guid driverId,
        DateOnly date,
        DutyStatus duty,
        decimal onDutyHours,
        decimal drivingHours,
        decimal offDutyHours,
        string? note) =>
        Record(
            tenantId, driverId, date, duty, onDutyHours, drivingHours, offDutyHours,
            HosLogEntrySource.DriverApp, enteredBy: null, note);

    private static Result<HosLogEntry> Record(
        Guid tenantId,
        Guid driverId,
        DateOnly date,
        DutyStatus duty,
        decimal onDutyHours,
        decimal drivingHours,
        decimal offDutyHours,
        HosLogEntrySource source,
        string? enteredBy,
        string? note)
    {
        if (!Enum.IsDefined(duty))
        {
            return Result.Failure<HosLogEntry>(HosErrors.InvalidDuty);
        }

        if (OutOfRange(onDutyHours) || OutOfRange(drivingHours) || OutOfRange(offDutyHours))
        {
            return Result.Failure<HosLogEntry>(HosErrors.HoursOutOfRange);
        }

        var entry = new HosLogEntry
        {
            TenantId = tenantId,
            DriverId = driverId,
            Date = date,
            Duty = duty,
            OnDutyHours = onDutyHours,
            DrivingHours = drivingHours,
            OffDutyHours = offDutyHours,
            Source = source,
            EnteredBy = enteredBy,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            RecordedAtUtc = DateTimeOffset.UtcNow,
        };

        entry.Raise(new HosDutyEntryRecordedDomainEvent(entry.Id, driverId, tenantId));
        return Result.Success(entry);
    }

    private static bool OutOfRange(decimal hours) => hours < MinHours || hours > MaxHours;
}
