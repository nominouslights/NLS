using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.RegulatoryBasis;

/// <summary>
/// One entry of the regulatory-basis catalogue: a stable <see cref="Key"/> (<c>HTA.318.6.1</c>,
/// <c>NSC.13.16</c>, <c>MR.95-2008.7.3</c>, <c>NLSP02.5.4</c>), the instrument and citation, the
/// verbatim <see cref="Quotation"/> where the Safety Plan quotes one, a Grade-8
/// <see cref="PlainExplanation"/>, and the <see cref="Consequence"/> the plan states for breaking
/// it. <b>Reference data, not tenant-scoped</b>: the law is the same for every carrier, so the
/// table carries no tenant_id and is readable by every tenant (RLS <c>USING (true)</c>) while
/// only the platform's system session may write it. A plain <see cref="Entity"/> rather than an
/// aggregate — no audit pipeline, no concurrency token, upserted by key from
/// <c>RegulatoryBasisSeed</c>.
/// </summary>
public sealed class RegulatoryBasis : Entity
{
    public const int KeyMaxLength = 64;
    public const int CitationMaxLength = 200;
    public const int TitleMaxLength = 300;
    public const int JurisdictionMaxLength = 16;

    private RegulatoryBasis()
    {
        // EF Core materialization only.
        Key = null!;
        Jurisdiction = null!;
        Citation = null!;
        Title = null!;
        PlainExplanation = null!;
    }

    public string Key { get; private set; }

    /// <summary>"CA-MB" for everything Manitoba; "CA" for federal instruments and Transport Canada guidance.</summary>
    public string Jurisdiction { get; private set; }

    public RegulatoryInstrument Instrument { get; private set; }

    /// <summary>The section reference as a person would cite it: "s. 318.6(1)", "Part A s. 1(c)", "M.R. 95/2008 s. 7(3)".</summary>
    public string Citation { get; private set; }

    public string Title { get; private set; }

    /// <summary>Verbatim text as quoted in NL-SP-02, or null where the plan gives no quotation.</summary>
    public string? Quotation { get; private set; }

    /// <summary>One or two plain sentences a new hire can read.</summary>
    public string PlainExplanation { get; private set; }

    /// <summary>What the plan says happens on a breach, where it says so (fine, suspension, out-of-service).</summary>
    public string? Consequence { get; private set; }

    public string? SourceUrl { get; private set; }

    /// <summary>
    /// Where the plan's citation differs from the current consolidation (a renumbered section, a
    /// repealed regulation, a renamed instrument), the plan's original wording — so the owner can
    /// correct NL-SP-02 and so an auditor reading the plan can still find the entry.
    /// </summary>
    public string? PlanCitationNote { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    /// <summary>The CA-MB rule-set version this entry was catalogued with.</summary>
    public int RuleSetVersion { get; private set; }

    public static RegulatoryBasis Create(RegulatoryBasisDefinition definition) =>
        new()
        {
            Key = definition.Key,
            Jurisdiction = definition.Jurisdiction,
            Instrument = definition.Instrument,
            Citation = definition.Citation,
            Title = definition.Title,
            Quotation = definition.Quotation,
            PlainExplanation = definition.PlainExplanation,
            Consequence = definition.Consequence,
            SourceUrl = definition.SourceUrl,
            PlanCitationNote = definition.PlanCitationNote,
            EffectiveFrom = definition.EffectiveFrom,
            RuleSetVersion = definition.RuleSetVersion,
        };

    /// <summary>Overwrites every field from the seed — the upsert path when a catalogue entry is corrected.</summary>
    public void Overwrite(RegulatoryBasisDefinition definition)
    {
        Jurisdiction = definition.Jurisdiction;
        Instrument = definition.Instrument;
        Citation = definition.Citation;
        Title = definition.Title;
        Quotation = definition.Quotation;
        PlainExplanation = definition.PlainExplanation;
        Consequence = definition.Consequence;
        SourceUrl = definition.SourceUrl;
        PlanCitationNote = definition.PlanCitationNote;
        EffectiveFrom = definition.EffectiveFrom;
        RuleSetVersion = definition.RuleSetVersion;
    }
}

/// <summary>The in-code shape of a catalogue entry (see <c>RegulatoryBasisSeed</c>).</summary>
public sealed record RegulatoryBasisDefinition(
    string Key,
    string Jurisdiction,
    RegulatoryInstrument Instrument,
    string Citation,
    string Title,
    string? Quotation,
    string PlainExplanation,
    string? Consequence,
    string? SourceUrl,
    string? PlanCitationNote,
    DateOnly EffectiveFrom,
    int RuleSetVersion);
