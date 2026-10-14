using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.RuleSets;

namespace NorthernLink.Compliance.Application.RuleSets;

/// <summary>The rule set in force for a tenant as of a date, with where it came from.</summary>
public sealed record ActiveRuleSet(
    RuleSource Source,
    RuleSetDocument Document,
    Guid? RuleSetId,
    Guid? ApplicationId,
    DateTimeOffset? AppliedAtUtc,
    string? ChangeNotice);

/// <summary>
/// Shared by the two rule queries: the newest application at or before the as-of date wins;
/// with none, the in-code default answers and says so. A published row whose id an application
/// names but that no longer resolves (never expected — rows are never deleted) also falls back
/// to the default rather than failing a help screen.
/// </summary>
public sealed class ActiveRuleSetResolver(IRuleSetRepository ruleSets, ITenantRuleSetApplicationRepository applications)
{
    public async Task<ActiveRuleSet> ResolveAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        // End of the as-of day, UTC: an application made at any time that day counts for it.
        var asOfUtc = new DateTimeOffset(asOf.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        var application = await applications.LatestAsOfAsync(asOfUtc, cancellationToken);
        if (application is not null)
        {
            var ruleSet = await ruleSets.GetAsync(application.RuleSetId, cancellationToken);
            if (ruleSet is not null)
            {
                return new ActiveRuleSet(
                    RuleSource.Applied, ruleSet.ToDocument(), ruleSet.Id, application.Id, application.AppliedAtUtc, application.ChangeNotice);
            }
        }

        return new ActiveRuleSet(RuleSource.Default, CaMbRuleSets.Current, null, null, null, null);
    }
}
