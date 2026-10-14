using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Authority.Events;
using NorthernLink.Compliance.Domain.Insurance.Events;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Domain.Records.Events;
using NorthernLink.Compliance.Domain.Registrations.Events;
using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Compliance;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Application;

/// <summary>
/// Compliance's explicit domain-event → integration-event translation. Two public contracts:
/// <list type="bullet">
/// <item>a registered record announces its issued number
/// (<see cref="ComplianceRecordRegisteredIntegrationEvent"/>) so the producer can stamp it;</item>
/// <item>this module's <em>own</em> R20 documents — an SFC, an insurance certificate, a
/// registration — are registered the way every other producer's are: a
/// <see cref="ComplianceRecordProducedIntegrationEvent"/> in Compliance's own outbox, which the
/// intake polls (<c>"compliance"</c> is one of the record-producer schemas). A renewal names its
/// predecessor in <c>SupersedesSourceRecordId</c>.</item>
/// </list>
/// Holds, anchors, disposal, rule-set applications and the rest stay internal.
/// </summary>
public sealed class ComplianceIntegrationEventMapper : IIntegrationEventMapper
{
    private static readonly string R20 = ComplianceRecordType.R20.ToString();

    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) => domainEvent switch
    {
        ComplianceRecordRegisteredDomainEvent e => new ComplianceRecordRegisteredIntegrationEvent(
            e.TenantId, e.RecordId, e.DocumentNumber, e.RecordType.ToString(), e.SourceModule, e.SourceRecordId),

        SafetyFitnessCertificateRecordedDomainEvent e => Produced(
            e.TenantId,
            e.Certificate.SourceRecordId,
            $"Safety Fitness Certificate {e.Certificate.Number} (valid {e.Certificate.IssuedOn:yyyy-MM-dd} to {e.Certificate.ExpiresOn:yyyy-MM-dd})",
            RecordSubjectKind.Carrier,
            subjectId: null,
            subjectDisplay: "Carrier",
            recordDate: e.Certificate.IssuedOn,
            [BasisKeys.Hta312_2_1, BasisKeys.MtiSfc, BasisKeys.Plan2],
            e.SupersedesSourceRecordId),

        InsurancePolicyRecordedDomainEvent e => Produced(
            e.TenantId,
            ComplianceSourceIds.InsurancePolicy(e.PolicyId),
            $"Insurance certificate — {e.Insurer} policy {e.PolicyNumber} — Unit {e.VehicleUnitNumber} ({e.EffectiveOn:yyyy-MM-dd} to {e.ExpiresOn:yyyy-MM-dd})",
            RecordSubjectKind.Vehicle,
            e.VehicleId,
            $"Unit {e.VehicleUnitNumber}",
            e.EffectiveOn,
            [BasisKeys.Hta312_2_7, BasisKeys.Mr132_2024_7_1_b, BasisKeys.Plan12_1],
            e.SupersedesSourceRecordId),

        VehicleRegistrationRecordedDomainEvent e => Produced(
            e.TenantId,
            ComplianceSourceIds.VehicleRegistration(e.RegistrationId),
            $"Registration — plate {e.Plate} (class {e.VehicleClass}) — Unit {e.VehicleUnitNumber} ({e.EffectiveOn:yyyy-MM-dd} to {e.ExpiresOn:yyyy-MM-dd})",
            RecordSubjectKind.Vehicle,
            e.VehicleId,
            $"Unit {e.VehicleUnitNumber}",
            e.EffectiveOn,
            [BasisKeys.Plan5_3, BasisKeys.Mr76_94_3_2_a_i],
            e.SupersedesSourceRecordId),

        _ => null,
    };

    private static ComplianceRecordProducedIntegrationEvent Produced(
        Guid tenantId,
        string sourceRecordId,
        string title,
        RecordSubjectKind subjectKind,
        Guid? subjectId,
        string subjectDisplay,
        DateOnly recordDate,
        IReadOnlyList<string> basisKeys,
        string? supersedesSourceRecordId) =>
        new(
            tenantId,
            ComplianceSourceIds.Module,
            sourceRecordId,
            R20,
            title.Length > ComplianceRecord.TitleMaxLength ? title[..ComplianceRecord.TitleMaxLength] : title,
            subjectKind.ToString(),
            subjectId,
            subjectDisplay,
            new Dictionary<string, DateOnly> { [nameof(AnchorKind.RecordDate)] = recordDate },
            SupportingLinks: null,
            basisKeys,
            PrintTemplateKey: null,
            supersedesSourceRecordId);
}
