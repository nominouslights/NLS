using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.Findings.Open;

public sealed class OpenComplianceFindingCommandHandler(
    IComplianceFindingRepository findings,
    IDriverRepository drivers,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<OpenComplianceFindingCommand, ComplianceFindingResponse>
{
    public async Task<Result<ComplianceFindingResponse>> Handle(OpenComplianceFindingCommand command, CancellationToken cancellationToken)
    {
        if (actor.UserId is not { } userId || !actor.HoldsAnyRole(Roles.ComplianceAccess))
        {
            return Result.Failure<ComplianceFindingResponse>(ComplianceErrors.ComplianceOfficerRequired);
        }

        if (await drivers.GetByIdAsync(command.DriverId, cancellationToken) is null)
        {
            return Result.Failure<ComplianceFindingResponse>(DriverErrors.NotFound);
        }

        var result = ComplianceFinding.Open(
            command.TenantId,
            command.DriverId,
            command.LocalDate,
            command.Rule,
            command.Severity,
            command.Description,
            command.Source,
            userId,
            clock.GetUtcNow(),
            command.FollowUpNote);

        if (result.IsFailure)
        {
            return Result.Failure<ComplianceFindingResponse>(result.Error);
        }

        findings.Add(result.Value);
        await findings.SaveChangesAsync(cancellationToken);
        return Result.Success(ComplianceResponseMapper.ToResponse(result.Value));
    }
}
