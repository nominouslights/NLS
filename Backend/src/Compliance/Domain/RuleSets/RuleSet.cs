using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.RuleSets;

/// <summary>
/// A published version of a jurisdiction's rules — reference data, not tenant-scoped (the law
/// is the same for every carrier in Manitoba), so a plain <see cref="Entity"/> with no audit
/// pipeline: rows are published once and never edited (a correction is a new version).
/// Readable by every tenant under RLS <c>USING (true)</c>; written only by the system session.
/// A tenant adopts a version through <see cref="TenantRuleSetApplication"/>.
/// </summary>
public sealed class RuleSet : Entity
{
    public const int JurisdictionMaxLength = 16;

    private RuleSet()
    {
        // EF Core materialization only.
        Jurisdiction = null!;
        Hos = null!;
        Retention = null!;
        Inspection = null!;
        DriverQualification = null!;
        Filing = null!;
        Thresholds = null!;
        Insurance = null!;
    }

    public string Jurisdiction { get; private set; }
    public int Version { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateTimeOffset PublishedAtUtc { get; private set; }
    public HosRuleSection Hos { get; private set; }
    public RetentionRuleSection Retention { get; private set; }
    public InspectionRuleSection Inspection { get; private set; }
    public DriverQualificationRuleSection DriverQualification { get; private set; }
    public FilingRuleSection Filing { get; private set; }
    public ThresholdsRuleSection Thresholds { get; private set; }
    public InsuranceRuleSection Insurance { get; private set; }

    /// <summary>Null on rows published before version 2 — read through <see cref="RuleSetDocument.EffectiveAuthority"/>.</summary>
    public AuthorityRuleSection? Authority { get; private set; }

    public static RuleSet Publish(RuleSetDocument document, DateTimeOffset now) =>
        new()
        {
            Jurisdiction = document.Jurisdiction,
            Version = document.Version,
            EffectiveFrom = document.EffectiveFrom,
            PublishedAtUtc = now,
            Hos = document.Hos,
            Retention = document.Retention,
            Inspection = document.Inspection,
            DriverQualification = document.DriverQualification,
            Filing = document.Filing,
            Thresholds = document.Thresholds,
            Insurance = document.Insurance,
            Authority = document.Authority,
        };

    public RuleSetDocument ToDocument() => new(
        Jurisdiction, Version, EffectiveFrom, Hos, Retention, Inspection, DriverQualification, Filing, Thresholds, Insurance, Authority);
}
