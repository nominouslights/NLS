using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Record;

/// <summary>
/// Records an HOS entry for a driver. <paramref name="Source"/> says which path it came from —
/// a dispatcher paper backup (<see cref="HosLogEntrySource.ManualPaperBackup"/>) or a Driver
/// Field App submission (<see cref="HosLogEntrySource.DriverApp"/>). The endpoint resolves it
/// from the request's friendly string and defaults to ManualPaperBackup when absent, which is
/// what the Dispatch Console sends. <paramref name="EnteredBy"/> is required on the manual path
/// and forced to null on the driver-app one — see <see cref="HosLogEntry"/>'s remarks.
/// Returns the new entry's id.
/// </summary>
public sealed record RecordHosEntryCommand(
    Guid TenantId,
    Guid DriverId,
    DateOnly Date,
    DutyStatus Duty,
    decimal OnDutyHours,
    decimal DrivingHours,
    decimal OffDutyHours,
    HosLogEntrySource Source,
    string? EnteredBy,
    string? Note) : ICommand<Guid>;
