using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Record;

/// <summary>
/// Records a manual (dispatcher paper-backup) HOS entry for a driver. The source is fixed
/// to <see cref="HosLogEntrySource.ManualPaperBackup"/> by the handler — a dispatcher entry
/// is a paper backup by definition; the driver-app source arrives via its own (deferred)
/// ingestion path. Returns the new entry's id.
/// </summary>
public sealed record RecordHosEntryCommand(
    Guid TenantId,
    Guid DriverId,
    DateOnly Date,
    DutyStatus Duty,
    decimal OnDutyHours,
    decimal DrivingHours,
    decimal OffDutyHours,
    string? EnteredBy,
    string? Note) : ICommand<Guid>;
