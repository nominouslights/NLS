using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.RuleSets;

/// <summary>
/// Publishes every in-code rule set (<c>CaMbRuleSets.All</c>) that is not yet in
/// <c>rule_sets</c>. Idempotent by (jurisdiction, version): a re-run inserts nothing and
/// reports <c>Created = false</c> per set. No tenant — the table is reference data.
/// </summary>
public sealed record PublishRuleSetsCommand(Guid? ActorId) : ICommand<RuleSetPublishResult>;

/// <summary>
/// Records that the tenant adopts a published rule set from now on. <paramref name="RuleSetId"/>
/// null means the newest published CA-MB version. Returns the application row's id.
/// </summary>
public sealed record ApplyTenantRuleSetCommand(Guid TenantId, Guid? RuleSetId, string? ChangeNotice, Guid? ActorId) : ICommand<Guid>;

/// <summary>The full typed rule set in force for the tenant as of <paramref name="AsOf"/> (end of that day, UTC).</summary>
public sealed record GetActiveRulesQuery(Guid TenantId, DateOnly AsOf) : IQuery<ActiveRulesResponse>;

/// <summary>One value of the rule set in force, by stable key.</summary>
public sealed record GetRuleValueQuery(Guid TenantId, string Key, DateOnly AsOf) : IQuery<RuleValueResponse>;

/// <summary>Every published rule set, newest first.</summary>
public sealed record ListRuleSetsQuery : IQuery<IReadOnlyList<RuleSetSummaryResponse>>;
