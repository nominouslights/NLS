using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Application.Hos.Ledger;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Application.Drivers.AttestPriorRecords;

public sealed class AttestPriorRecordsCommandHandler(
    IDriverRepository drivers,
    ICurrentActor actor,
    TimeProvider clock)
    : ICommandHandler<AttestPriorRecordsCommand, PriorRecordsResponse>
{
    public async Task<Result<PriorRecordsResponse>> Handle(AttestPriorRecordsCommand command, CancellationToken cancellationToken)
    {
        if (actor.UserId is not { } userId || !actor.HoldsAnyRole(Roles.ComplianceAccess))
        {
            return Result.Failure<PriorRecordsResponse>(ComplianceErrors.ComplianceOfficerRequired);
        }

        var driver = await drivers.GetByIdAsync(command.DriverId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure<PriorRecordsResponse>(DriverErrors.NotFound);
        }

        var result = driver.AttestPriorRecords(
            command.CoveredThroughDate,
            userId,
            command.Note,
            command.WorksForOtherCarriers,
            clock.GetUtcNow(),
            DutyLedgerAssembler.Calendar);

        if (result.IsFailure)
        {
            return Result.Failure<PriorRecordsResponse>(result.Error);
        }

        // An identical re-attestation raises nothing and is left Unchanged — a no-op save.
        await drivers.SaveChangesAsync(cancellationToken);

        return Result.Success(new PriorRecordsResponse(
            driver.PriorRecordsCoveredThroughDate!.Value,
            driver.PriorRecordsAttestedAtUtc!.Value,
            driver.PriorRecordsAttestedByUserId!.Value,
            driver.PriorRecordsNote,
            driver.WorksForOtherCarriers));
    }
}
