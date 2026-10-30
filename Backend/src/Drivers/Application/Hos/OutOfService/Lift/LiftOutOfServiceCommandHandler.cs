using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.OutOfService.Lift;

public sealed class LiftOutOfServiceCommandHandler(
    IDriverOutOfServiceRepository records,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<LiftOutOfServiceCommand, OutOfServiceRecordResponse>
{
    public async Task<Result<OutOfServiceRecordResponse>> Handle(LiftOutOfServiceCommand command, CancellationToken cancellationToken)
    {
        if (actor.UserId is not { } userId || !actor.HoldsAnyRole(Roles.ComplianceAccess))
        {
            return Result.Failure<OutOfServiceRecordResponse>(ComplianceErrors.ComplianceOfficerRequired);
        }

        var record = await records.GetAsync(command.RecordId, cancellationToken);
        if (record is null || record.DriverId != command.DriverId)
        {
            return Result.Failure<OutOfServiceRecordResponse>(ComplianceErrors.OutOfServiceNotFound);
        }

        var now = clock.GetUtcNow();
        var result = record.Lift(userId, now, command.Reason);
        if (result.IsFailure)
        {
            return Result.Failure<OutOfServiceRecordResponse>(result.Error);
        }

        await records.SaveChangesAsync(cancellationToken);
        return Result.Success(ComplianceResponseMapper.ToResponse(record, now));
    }
}
