using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.Findings.Update;

public sealed class UpdateComplianceFindingCommandHandler(
    IComplianceFindingRepository findings,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<UpdateComplianceFindingCommand, ComplianceFindingResponse>
{
    public async Task<Result<ComplianceFindingResponse>> Handle(UpdateComplianceFindingCommand command, CancellationToken cancellationToken)
    {
        if (actor.UserId is not { } userId || !actor.HoldsAnyRole(Roles.ComplianceAccess))
        {
            return Result.Failure<ComplianceFindingResponse>(ComplianceErrors.ComplianceOfficerRequired);
        }

        var finding = await findings.GetAsync(command.FindingId, cancellationToken);
        if (finding is null || finding.DriverId != command.DriverId)
        {
            return Result.Failure<ComplianceFindingResponse>(ComplianceErrors.FindingNotFound);
        }

        var result = finding.Update(command.Status, command.FollowUpNote, userId, clock.GetUtcNow());
        if (result.IsFailure)
        {
            return Result.Failure<ComplianceFindingResponse>(result.Error);
        }

        await findings.SaveChangesAsync(cancellationToken);
        return Result.Success(ComplianceResponseMapper.ToResponse(finding));
    }
}
