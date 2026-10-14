using NorthernLink.Compliance.Domain.RuleSets;

namespace NorthernLink.Compliance.Application.RuleSets;

/// <summary>Where the rules a tenant sees came from.</summary>
public enum RuleSource
{
    /// <summary>A published rule set the tenant applied (<c>tenant_rule_set_applications</c>).</summary>
    Applied,

    /// <summary>No application row as of the date — the in-code CA-MB default answered.</summary>
    Default,
}

/// <summary>A published rule set, summarized (no sections).</summary>
public sealed record RuleSetSummaryResponse(Guid Id, string Jurisdiction, int Version, DateOnly EffectiveFrom, DateTimeOffset PublishedAtUtc);

/// <summary>
/// The full rule set in force for a tenant as of a date: typed sections for a help screen, and
/// the flat <see cref="Values"/> list for anything that looks a value up by key.
/// </summary>
public sealed record ActiveRulesResponse(
    RuleSource Source,
    Guid? RuleSetId,
    Guid? ApplicationId,
    DateTimeOffset? AppliedAtUtc,
    string? ChangeNotice,
    string Jurisdiction,
    int Version,
    DateOnly EffectiveFrom,
    HosRuleSection Hos,
    RetentionRuleSection Retention,
    InspectionRuleSection Inspection,
    DriverQualificationRuleSection DriverQualification,
    FilingRuleSection Filing,
    ThresholdsRuleSection Thresholds,
    InsuranceRuleSection Insurance,
    AuthorityRuleSection Authority,
    IReadOnlyList<RuleValue> Values);

/// <summary>One rule value with its provenance.</summary>
public sealed record RuleValueResponse(string Key, string Value, string Unit, RuleSource Source, string Jurisdiction, int Version);

/// <summary>Body of POST rule-sets/publish: every in-code set, with whether this call inserted it.</summary>
public sealed record RuleSetPublishResult(IReadOnlyList<RuleSetPublishedResponse> RuleSets);

public sealed record RuleSetPublishedResponse(Guid Id, string Jurisdiction, int Version, bool Created);
