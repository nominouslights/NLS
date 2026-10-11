using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Hos.Exceptions.Withdraw;

public sealed class WithdrawHosExceptionCommandHandler(
    IHosExceptionRepository exceptions,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<WithdrawHosExceptionCommand, HosExceptionResponse>
{
    public async Task<Result<HosExceptionResponse>> Handle(WithdrawHosExceptionCommand command, CancellationToken cancellationToken)
    {
        if (actor.UserId is not { } userId)
        {
            return Result.Failure<HosExceptionResponse>(ComplianceErrors.ActorRequired);
        }

        var exception = await exceptions.GetAsync(command.ExceptionId, cancellationToken);
        if (exception is null || exception.DriverId != command.DriverId)
        {
            return Result.Failure<HosExceptionResponse>(ComplianceErrors.ExceptionNotFound);
        }

        var result = exception.Withdraw(
            userId, clock.GetUtcNow(), complianceOfficer: actor.HoldsAnyRole(Roles.ComplianceAccess), command.Note);
        if (result.IsFailure)
        {
            return Result.Failure<HosExceptionResponse>(result.Error);
        }

        await exceptions.SaveChangesAsync(cancellationToken);
        return Result.Success(ComplianceResponseMapper.ToResponse(exception));
    }
}
