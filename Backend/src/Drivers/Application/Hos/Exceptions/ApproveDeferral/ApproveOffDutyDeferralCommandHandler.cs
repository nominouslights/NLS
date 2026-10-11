using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.Exceptions.ApproveDeferral;

public sealed class ApproveOffDutyDeferralCommandHandler(
    IHosExceptionRepository exceptions,
    IDriverRepository drivers,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<ApproveOffDutyDeferralCommand, HosExceptionResponse>
{
    public async Task<Result<HosExceptionResponse>> Handle(ApproveOffDutyDeferralCommand command, CancellationToken cancellationToken)
    {
        // The route policy already says ComplianceAccess; this is the handler's own proof, so
        // the rule holds even if the command is ever dispatched from somewhere else.
        if (actor.UserId is not { } userId || !actor.HoldsAnyRole(Roles.ComplianceAccess))
        {
            return Result.Failure<HosExceptionResponse>(ComplianceErrors.DeferralRequiresComplianceOfficer);
        }

        if (await drivers.GetByIdAsync(command.DriverId, cancellationToken) is null)
        {
            return Result.Failure<HosExceptionResponse>(DriverErrors.NotFound);
        }

        var result = HosException.ApproveDeferral(
            command.TenantId,
            command.DriverId,
            userId,
            clock.GetUtcNow(),
            command.TripId,
            command.EffectiveFromUtc,
            command.DeferredOffDuty,
            command.Reason,
            command.Location,
            command.EldRemarkRecorded,
            DutyLedgerAssembler.Calendar);

        if (result.IsFailure)
        {
            return Result.Failure<HosExceptionResponse>(result.Error);
        }

        exceptions.Add(result.Value);
        await exceptions.SaveChangesAsync(cancellationToken);
        return Result.Success(ComplianceResponseMapper.ToResponse(result.Value));
    }
}
