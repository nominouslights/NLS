using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.EldMalfunctions.Resolve;

public sealed class ResolveEldMalfunctionCommandHandler(
    IEldMalfunctionRepository reports,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<ResolveEldMalfunctionCommand, EldMalfunctionResponse>
{
    public async Task<Result<EldMalfunctionResponse>> Handle(ResolveEldMalfunctionCommand command, CancellationToken cancellationToken)
    {
        if (actor.UserId is not { } userId || !actor.HoldsAnyRole(Roles.ComplianceAccess))
        {
            return Result.Failure<EldMalfunctionResponse>(ComplianceErrors.ComplianceOfficerRequired);
        }

        var report = await reports.GetAsync(command.ReportId, cancellationToken);
        if (report is null || report.DriverId != command.DriverId)
        {
            return Result.Failure<EldMalfunctionResponse>(ComplianceErrors.EldMalfunctionNotFound);
        }

        var result = report.Resolve(userId, clock.GetUtcNow(), command.Note);
        if (result.IsFailure)
        {
            return Result.Failure<EldMalfunctionResponse>(result.Error);
        }

        await reports.SaveChangesAsync(cancellationToken);
        return Result.Success(ComplianceResponseMapper.ToResponse(report));
    }
}
