using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Application.Hos.Ledger;

/// <summary>
/// One driver's compliance records as the ledger assembler and the reads consume them — every
/// status, so a read can show a rejected exception; <see cref="DutyLedgerAssembler"/> picks
/// the ones that apply. The attestation fields come from the <c>Driver</c> row.
/// </summary>
public sealed record ComplianceRecords(
    IReadOnlyList<HosException> Exceptions,
    IReadOnlyList<DriverOutOfService> OutOfService,
    IReadOnlyList<EldMalfunctionReport> Malfunctions,
    IReadOnlyList<ComplianceFinding> Findings,
    DateOnly? PriorRecordsCoveredThroughDate,
    DateTimeOffset? PriorRecordsAttestedAtUtc,
    Guid? PriorRecordsAttestedByUserId,
    string? PriorRecordsNote,
    bool WorksForOtherCarriers)
{
    /// <summary>Nothing on file and no attestation.</summary>
    public static readonly ComplianceRecords None = new([], [], [], [], null, null, null, null, false);

    public bool IsAttested => PriorRecordsCoveredThroughDate is not null;
}
