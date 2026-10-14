namespace NorthernLink.Compliance.Domain.RuleSets;

/// <summary>
/// A whole rule set as one value: jurisdiction, version, effective date and the seven typed
/// sections. The in-code defaults (<see cref="CaMbRuleSets"/>) produce one; a published
/// <see cref="RuleSet"/> row stores one section per jsonb column and rebuilds it with
/// <see cref="RuleSet.ToDocument"/>.
/// </summary>
public sealed record RuleSetDocument(
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
    AuthorityRuleSection? Authority = null)
{
    /// <summary>
    /// The authority section in force: the set's own, or <see cref="AuthorityRuleSection.Default"/>
    /// for a version published before the section existed (version 1).
    /// </summary>
    public AuthorityRuleSection EffectiveAuthority => Authority ?? AuthorityRuleSection.Default;

    /// <summary>Every value in the set, each under its stable key.</summary>
    public IReadOnlyList<RuleValue> Flatten() =>
        Hos.Flatten()
            .Concat(Retention.Flatten())
            .Concat(Inspection.Flatten())
            .Concat(DriverQualification.Flatten())
            .Concat(Filing.Flatten())
            .Concat(Thresholds.Flatten())
            .Concat(Insurance.Flatten())
            .Concat(EffectiveAuthority.Flatten())
            .ToList();

    /// <summary>The value under <paramref name="key"/>, or null when no such key exists in this set.</summary>
    public RuleValue? Resolve(string key) =>
        Flatten().FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.Ordinal));
}
