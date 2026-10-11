using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.OutOfService.Record;

/// <summary>
/// Records the declaration and opens its finding together. The finding is created here, in the
/// same unit of work, rather than by a journal-driven reaction: Drivers has no <c>OnEvent</c>
/// bindings, and one transaction needs no idempotency key — the two rows exist together or not
/// at all. Both repositories share the module's DbContext, so one save commits both.
/// </summary>
public sealed class RecordOutOfServiceCommandHandler(
    IDriverOutOfServiceRepository records,
    IComplianceFindingRepository findings,
    IDriverRepository drivers,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<RecordOutOfServiceCommand, OutOfServiceRecordResponse>
{
    public async Task<Result<OutOfServiceRecordResponse>> Handle(RecordOutOfServiceCommand command, CancellationToken cancellationToken)
    {
        if (actor.UserId is not { } userId || !actor.HoldsAnyRole(Roles.ComplianceAccess))
        {
            return Result.Failure<OutOfServiceRecordResponse>(ComplianceErrors.ComplianceOfficerRequired);
        }

        if (await drivers.GetByIdAsync(command.DriverId, cancellationToken) is null)
        {
            return Result.Failure<OutOfServiceRecordResponse>(DriverErrors.NotFound);
        }

        var now = clock.GetUtcNow();
        var active = await records.ActiveForDriverAsync(command.DriverId, cancellationToken);
        if (active.Any(r => r.IsActiveAt(now)))
        {
            return Result.Failure<OutOfServiceRecordResponse>(ComplianceErrors.AlreadyOutOfService);
        }

        var result = DriverOutOfService.Record(
            command.TenantId,
            command.DriverId,
            command.DeclaredAtUtc,
            userId,
            now,
            command.UntilUtc,
            command.OpenEnded,
            command.Reason,
            command.Evidence);

        if (result.IsFailure)
        {
            return Result.Failure<OutOfServiceRecordResponse>(result.Error);
        }

        var record = result.Value;
        var finding = ComplianceFinding.Open(
            command.TenantId,
            command.DriverId,
            DutyLedgerAssembler.Calendar.DayOf(record.DeclaredAtUtc.ToUnixTimeSeconds()),
            ComplianceFinding.OutOfServiceDeclaredRule,
            FindingSeverity.Severe,
            $"Declared out of service by enforcement at {record.DeclaredAtUtc:yyyy-MM-dd HH:mm} UTC: {record.Reason}",
            FindingSource.Auto,
            userId,
            now);

        if (finding.IsFailure)
        {
            return Result.Failure<OutOfServiceRecordResponse>(finding.Error);
        }

        records.Add(record);
        findings.Add(finding.Value);
        await records.SaveChangesAsync(cancellationToken);

        return Result.Success(ComplianceResponseMapper.ToResponse(record, now));
    }
}
