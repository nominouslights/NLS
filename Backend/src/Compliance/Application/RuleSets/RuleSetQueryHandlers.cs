using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.RuleSets;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.RuleSets;

/// <summary>
/// Handles <see cref="GetActiveRulesQuery"/> — NL-SP-02 ss. 4, 5, 3, 11, 12 and 13: the rules the
/// carrier operates under, in one typed document, for help screens and the Monthly Compliance
/// Report. The HOS section is a display mirror; the engine in Shared enforces.
/// </summary>
public sealed class GetActiveRulesQueryHandler(ActiveRuleSetResolver resolver)
    : IQueryHandler<GetActiveRulesQuery, ActiveRulesResponse>
{
    public async Task<Result<ActiveRulesResponse>> Handle(GetActiveRulesQuery query, CancellationToken cancellationToken)
    {
        var active = await resolver.ResolveAsync(query.AsOf, cancellationToken);
        var d = active.Document;
        return Result.Success(new ActiveRulesResponse(
            active.Source,
            active.RuleSetId,
            active.ApplicationId,
            active.AppliedAtUtc,
            active.ChangeNotice,
            d.Jurisdiction,
            d.Version,
            d.EffectiveFrom,
            d.Hos,
            d.Retention,
            d.Inspection,
            d.DriverQualification,
            d.Filing,
            d.Thresholds,
            d.Insurance,
            d.EffectiveAuthority,
            d.Flatten()));
    }
}

/// <summary>Handles <see cref="GetRuleValueQuery"/> — one rule by stable key (<see cref="RuleKeyCatalogue"/>).</summary>
public sealed class GetRuleValueQueryHandler(ActiveRuleSetResolver resolver)
    : IQueryHandler<GetRuleValueQuery, RuleValueResponse>
{
    public async Task<Result<RuleValueResponse>> Handle(GetRuleValueQuery query, CancellationToken cancellationToken)
    {
        var key = query.Key?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            return Result.Failure<RuleValueResponse>(RuleSetErrors.RuleKeyRequired);
        }

        var active = await resolver.ResolveAsync(query.AsOf, cancellationToken);
        var value = active.Document.Resolve(key);
        if (value is null)
        {
            return Result.Failure<RuleValueResponse>(RuleSetErrors.RuleKeyUnknown);
        }

        return Result.Success(new RuleValueResponse(
            value.Key, value.Value, value.Unit, active.Source, active.Document.Jurisdiction, active.Document.Version));
    }
}

/// <summary>Handles <see cref="ListRuleSetsQuery"/> — every published version, newest first.</summary>
public sealed class ListRuleSetsQueryHandler(IRuleSetRepository ruleSets)
    : IQueryHandler<ListRuleSetsQuery, IReadOnlyList<RuleSetSummaryResponse>>
{
    public async Task<Result<IReadOnlyList<RuleSetSummaryResponse>>> Handle(ListRuleSetsQuery query, CancellationToken cancellationToken)
    {
        var rows = await ruleSets.ListAsync(cancellationToken);
        IReadOnlyList<RuleSetSummaryResponse> response = rows
            .OrderByDescending(r => r.PublishedAtUtc)
            .Select(r => new RuleSetSummaryResponse(r.Id, r.Jurisdiction, r.Version, r.EffectiveFrom, r.PublishedAtUtc))
            .ToList();
        return Result.Success(response);
    }
}
