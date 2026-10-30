using NorthernLink.Compliance.Domain.Numbering;

namespace NorthernLink.Compliance.Application.Settings;

/// <summary>The tenant's settings row on the wire.</summary>
public sealed record ComplianceSettingsResponse(
    Guid Id,
    string CarrierLegalName,
    string? NscNumber,
    string DocumentPrefix,
    string PrincipalPlaceOfBusiness,
    string? FileLocationNote,
    Guid? ModifiedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static ComplianceSettingsResponse From(TenantComplianceSettings s) => new(
        s.Id, s.CarrierLegalName, s.NscNumber, s.DocumentPrefix, s.PrincipalPlaceOfBusiness, s.FileLocationNote,
        s.ModifiedBy, s.CreatedAtUtc, s.UpdatedAtUtc);
}

/// <summary>What POST seed-defaults did — zeros everywhere on a re-run, which is a success.</summary>
public sealed record SeedDefaultsResult(
    bool SettingsCreated,
    bool RuleSetPublished,
    bool RuleSetApplied,
    int SeriesCreated,
    int RegulatoryBasisInserted);
