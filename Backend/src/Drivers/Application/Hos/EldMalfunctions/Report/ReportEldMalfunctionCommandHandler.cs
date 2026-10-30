using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.EldMalfunctions.Report;

public sealed class ReportEldMalfunctionCommandHandler(
    IEldMalfunctionRepository reports,
    IDriverRepository drivers,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<ReportEldMalfunctionCommand, EldMalfunctionResponse>
{
    public async Task<Result<EldMalfunctionResponse>> Handle(ReportEldMalfunctionCommand command, CancellationToken cancellationToken)
    {
        if (actor.UserId is not { } userId)
        {
            return Result.Failure<EldMalfunctionResponse>(ComplianceErrors.ActorRequired);
        }

        var driver = await drivers.GetByIdAsync(command.DriverId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure<EldMalfunctionResponse>(DriverErrors.NotFound);
        }

        var result = EldMalfunctionReport.Report(
            command.TenantId,
            command.DriverId,
            driver.Name,
            command.VehicleId,
            command.Code,
            command.NoticedAtUtc,
            userId,
            clock.GetUtcNow(),
            command.Description);

        if (result.IsFailure)
        {
            return Result.Failure<EldMalfunctionResponse>(result.Error);
        }

        reports.Add(result.Value);
        await reports.SaveChangesAsync(cancellationToken);
        return Result.Success(ComplianceResponseMapper.ToResponse(result.Value));
    }
}
