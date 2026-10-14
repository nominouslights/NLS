using Microsoft.Extensions.Logging;
using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Compliance;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Compliance.Application.Integration;

/// <summary>
/// An aggregate kind this module registers in its own record registry, found by the prefix of
/// the source record id it registered under (<see cref="ComplianceSourceIds"/>). The registered
/// event's answer is routed to the one target whose <see cref="SourceRecordIdPrefix"/> matches.
/// </summary>
public interface IRecordStampTarget
{
    string SourceRecordIdPrefix { get; }

    /// <summary>Stamps the record id and number; false when the aggregate is gone or already carries them.</summary>
    Task<bool> StampAsync(string sourceRecordId, Guid recordId, string documentNumber, CancellationToken cancellationToken);
}

/// <summary>
/// Compliance consuming its own answer: when the registry numbers a record this module produced
/// (an SFC, an insurance certificate, a registration), the number is stamped back on the
/// aggregate so prints and screens show it — "number pending" until then. Subscribed as fan-out
/// so a future producer module consuming the same key is unaffected. Records registered by
/// other modules (or by hand under another source module) are not this handler's business.
/// </summary>
public sealed class ComplianceRecordRegisteredIntegrationEventHandler(
    IEnumerable<IRecordStampTarget> targets,
    ILogger<ComplianceRecordRegisteredIntegrationEventHandler> logger)
    : IIntegrationEventHandler<ComplianceRecordRegisteredIntegrationEvent>
{
    public async Task Handle(ComplianceRecordRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (!string.Equals(integrationEvent.SourceModule, ComplianceSourceIds.Module, StringComparison.Ordinal))
        {
            return;
        }

        var target = targets.FirstOrDefault(t =>
            integrationEvent.SourceRecordId.StartsWith(t.SourceRecordIdPrefix, StringComparison.Ordinal));
        if (target is null)
        {
            // A record registered by hand under source module "compliance" — nothing to stamp.
            logger.LogWarning(
                "Compliance record {RecordId} ({DocumentNumber}) names source id {SourceRecordId}, which no stamp target owns; ignoring",
                integrationEvent.RecordId, integrationEvent.DocumentNumber, integrationEvent.SourceRecordId);
            return;
        }

        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            var stamped = await target.StampAsync(
                integrationEvent.SourceRecordId, integrationEvent.RecordId, integrationEvent.DocumentNumber, cancellationToken);

            logger.LogInformation(
                "Compliance {Action} {DocumentNumber} on {SourceRecordId} of tenant {TenantId} ({EventId})",
                stamped ? "stamped" : "already had (or could not find)",
                integrationEvent.DocumentNumber, integrationEvent.SourceRecordId, integrationEvent.TenantId, integrationEvent.EventId);
        }
    }
}

/// <summary>Stamps an SFC (current or historical) on the tenant's carrier authority.</summary>
public sealed class SfcRecordStampTarget(ICarrierAuthorityRepository authorities, IComplianceRecordRepository unitOfWork)
    : IRecordStampTarget
{
    public string SourceRecordIdPrefix => ComplianceSourceIds.SfcPrefix;

    public async Task<bool> StampAsync(string sourceRecordId, Guid recordId, string documentNumber, CancellationToken cancellationToken)
    {
        var authority = await authorities.GetAsync(cancellationToken);
        if (authority is null || !authority.StampRecord(sourceRecordId, recordId, documentNumber))
        {
            return false;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

/// <summary>Stamps an insurance policy.</summary>
public sealed class InsurancePolicyRecordStampTarget(IInsurancePolicyRepository policies, IComplianceRecordRepository unitOfWork)
    : IRecordStampTarget
{
    public string SourceRecordIdPrefix => ComplianceSourceIds.InsurancePolicyPrefix;

    public async Task<bool> StampAsync(string sourceRecordId, Guid recordId, string documentNumber, CancellationToken cancellationToken)
    {
        if (ComplianceSourceIds.IdAfter(SourceRecordIdPrefix, sourceRecordId) is not { } id)
        {
            return false;
        }

        var policy = await policies.GetByIdAsync(id, cancellationToken);
        if (policy is null || !policy.StampRecord(recordId, documentNumber))
        {
            return false;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

/// <summary>Stamps a vehicle registration.</summary>
public sealed class VehicleRegistrationRecordStampTarget(IVehicleRegistrationRepository registrations, IComplianceRecordRepository unitOfWork)
    : IRecordStampTarget
{
    public string SourceRecordIdPrefix => ComplianceSourceIds.VehicleRegistrationPrefix;

    public async Task<bool> StampAsync(string sourceRecordId, Guid recordId, string documentNumber, CancellationToken cancellationToken)
    {
        if (ComplianceSourceIds.IdAfter(SourceRecordIdPrefix, sourceRecordId) is not { } id)
        {
            return false;
        }

        var registration = await registrations.GetByIdAsync(id, cancellationToken);
        if (registration is null || !registration.StampRecord(recordId, documentNumber))
        {
            return false;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
