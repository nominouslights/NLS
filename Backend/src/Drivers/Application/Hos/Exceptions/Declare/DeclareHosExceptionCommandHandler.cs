using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.Exceptions.Declare;

public sealed class DeclareHosExceptionCommandHandler(
    IHosExceptionRepository exceptions,
    IDriverRepository drivers,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<DeclareHosExceptionCommand, HosExceptionResponse>
{
    public async Task<Result<HosExceptionResponse>> Handle(DeclareHosExceptionCommand command, CancellationToken cancellationToken)
    {
        if (actor.UserId is not { } userId)
        {
            return Result.Failure<HosExceptionResponse>(ComplianceErrors.ActorRequired);
        }

        if (await drivers.GetByIdAsync(command.DriverId, cancellationToken) is null)
        {
            return Result.Failure<HosExceptionResponse>(DriverErrors.NotFound);
        }

        var result = HosException.Declare(
            command.TenantId,
            command.DriverId,
            command.Kind,
            userId,
            clock.GetUtcNow(),
            command.EffectiveFromUtc,
            command.EffectiveToUtc,
            command.Reason,
            command.Location,
            command.NotKnownBeforeDeparture,
            command.ExtraDriving,
            command.EldRemarkRecorded,
            command.TripId);

        if (result.IsFailure)
        {
            return Result.Failure<HosExceptionResponse>(result.Error);
        }

        exceptions.Add(result.Value);
        await exceptions.SaveChangesAsync(cancellationToken);
        return Result.Success(ComplianceResponseMapper.ToResponse(result.Value));
    }
}
