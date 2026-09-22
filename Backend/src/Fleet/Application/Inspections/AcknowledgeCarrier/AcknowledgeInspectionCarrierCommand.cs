using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Inspections.AcknowledgeCarrier;

/// <summary>
/// Signs the NL-PTI-01 carrier acknowledgement line on one inspection: the carrier's
/// representative confirming they were shown a report carrying a Major or OutOfService defect.
/// One per report, and final — a second call fails with <c>CarrierAlreadyAcknowledged</c> rather
/// than re-stamping who signed and when.
/// </summary>
/// <param name="AcknowledgedBy">
/// Client-supplied, following the existing <c>EnteredBy</c>/<c>ResolvedBy</c> convention on this
/// aggregate rather than reading the JWT (server-side attribution is a documented follow-up
/// across the whole module). Unlike those two it has NO "Dispatch" fallback: this is a
/// signature, and an unnamed signature is rejected by the aggregate.
/// </param>
public sealed record AcknowledgeInspectionCarrierCommand(
    Guid TenantId,
    Guid InspectionId,
    string? AcknowledgedBy,
    string? Note) : ICommand;
