using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.Exceptions.Acknowledge;

public sealed class AcknowledgeHosExceptionCommandHandler(
    IHosExceptionRepository exceptions,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<AcknowledgeHosExceptionCommand, HosExceptionResponse>
{
    public async Task<Result<HosExceptionResponse>> Handle(AcknowledgeHosExceptionCommand command, CancellationToken cancellationToken)
    {
        // The route (HosAccess) admits the driver too; acknowledging is dispatch's or
        // compliance's act, so the handler is where that line is drawn.
        if (actor.UserId is not { } userId
            || !(actor.HoldsAnyRole(Roles.DispatchAccess) || actor.HoldsAnyRole(Roles.ComplianceAccess)))
        {
            return Result.Failure<HosExceptionResponse>(ComplianceErrors.AcknowledgeRequiresDispatch);
        }

        var exception = await exceptions.GetAsync(command.ExceptionId, cancellationToken);
        if (exception is null || exception.DriverId != command.DriverId)
        {
            return Result.Failure<HosExceptionResponse>(ComplianceErrors.ExceptionNotFound);
        }

        var result = exception.Acknowledge(userId, clock.GetUtcNow());
        if (result.IsFailure)
        {
            return Result.Failure<HosExceptionResponse>(result.Error);
        }

        // An already-acknowledged exception raises nothing and is left Unchanged — a no-op save.
        await exceptions.SaveChangesAsync(cancellationToken);
        return Result.Success(ComplianceResponseMapper.ToResponse(exception));
    }
}
