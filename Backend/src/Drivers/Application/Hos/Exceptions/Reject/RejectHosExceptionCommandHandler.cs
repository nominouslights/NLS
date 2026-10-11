using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.Exceptions.Reject;

public sealed class RejectHosExceptionCommandHandler(
    IHosExceptionRepository exceptions,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<RejectHosExceptionCommand, HosExceptionResponse>
{
    public async Task<Result<HosExceptionResponse>> Handle(RejectHosExceptionCommand command, CancellationToken cancellationToken)
    {
        if (actor.UserId is not { } userId || !actor.HoldsAnyRole(Roles.ComplianceAccess))
        {
            return Result.Failure<HosExceptionResponse>(ComplianceErrors.ComplianceOfficerRequired);
        }

        var exception = await exceptions.GetAsync(command.ExceptionId, cancellationToken);
        if (exception is null || exception.DriverId != command.DriverId)
        {
            return Result.Failure<HosExceptionResponse>(ComplianceErrors.ExceptionNotFound);
        }

        var result = exception.Reject(userId, clock.GetUtcNow(), command.Note);
        if (result.IsFailure)
        {
            return Result.Failure<HosExceptionResponse>(result.Error);
        }

        await exceptions.SaveChangesAsync(cancellationToken);
        return Result.Success(ComplianceResponseMapper.ToResponse(exception));
    }
}
