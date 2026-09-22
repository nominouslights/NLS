using NorthernLink.Drivers.Domain.Hos.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// One day's Hours-of-Service duty log for a driver — its own aggregate (child of Driver,
/// keyed on <see cref="DriverId"/>), added whole and never mutated. <see cref="Record"/> is the
/// general factory; <see cref="RecordManualEntry"/> and <see cref="RecordFromDriverApp"/> are
/// thin wrappers naming the two sources, mirroring Fleet's <c>VehicleInspection</c>.
///
/// <para>
/// <b>EnteredBy is source-dependent, and that is a real rule, not plumbing.</b> A manual
/// paper-backup entry is a dispatcher typing up a paper log, so it REQUIRES the dispatcher's
/// name — without it the record has no accountable author at all. A Driver Field App entry has no
/// dispatcher: the author is the authenticated driver, already recorded as
/// <see cref="DriverId"/> + <see cref="Source"/>. <see cref="Record"/> therefore FORCES
/// <see cref="EnteredBy"/> to null on the DriverApp path rather than rejecting a supplied value.
/// Two reasons: a free-text name on a self-submitted compliance record would be forgeable and
/// would read as authoritative in an audit while being worth nothing (the same argument as
/// <c>ICurrentActor</c>'s); and the Field App is offline-first, so a 400 on a replayed queue item
/// wedges the queue where dropping a field nobody should have sent does not. The payoff is that
/// EnteredBy stays legible: non-null if and only if a human dispatcher typed the entry.
/// </para>
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
        string? note) =>
        Record(
            tenantId, driverId, date, duty, onDutyHours, drivingHours, offDutyHours,
            HosLogEntrySource.ManualPaperBackup, enteredBy, note);

    /// <summary>
    /// A duty log submitted from the Driver Field App — the primary source. No dispatcher
    /// name (<see cref="EnteredBy"/> is null).
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

    /// <summary>
    /// The general factory — one source parameter, and the EnteredBy rule branching on it (see
    /// the type's remarks). The two wrappers above exist so existing call sites and tests keep
    /// naming their source rather than passing an enum everywhere.
    /// </summary>
    public static Result<HosLogEntry> Record(
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

        if (!Enum.IsDefined(source))
        {
            return Result.Failure<HosLogEntry>(HosErrors.InvalidSource);
        }

        if (OutOfRange(onDutyHours) || OutOfRange(drivingHours) || OutOfRange(offDutyHours))
        {
            return Result.Failure<HosLogEntry>(HosErrors.HoursOutOfRange);
        }

        string? author;
        if (source == HosLogEntrySource.ManualPaperBackup)
        {
            // A paper backup with no dispatcher name has no accountable author.
            if (string.IsNullOrWhiteSpace(enteredBy))
            {
                return Result.Failure<HosLogEntry>(HosErrors.EnteredByRequired);
            }

            author = enteredBy.Trim();
        }
        else
        {
            // Driver App: the author is the authenticated driver, not a typed name. Anything the
            // client sent here is dropped rather than rejected — see the type's remarks.
            author = null;
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
            EnteredBy = author,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            RecordedAtUtc = DateTimeOffset.UtcNow,
        };

        entry.Raise(new HosDutyEntryRecordedDomainEvent(entry.Id, driverId, tenantId));
        return Result.Success(entry);
    }

    private static bool OutOfRange(decimal hours) => hours < MinHours || hours > MaxHours;
}
