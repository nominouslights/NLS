using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.RuleSets;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.RuleSets;

/// <summary>
/// Handles <see cref="ApplyTenantRuleSetCommand"/> — NL-SP-02 s. 13: a change to the rules the
/// carrier operates under is a dated, noticed event, so the next Monthly Compliance Report
/// (NL-CM-01) can say what changed.
/// </summary>
public sealed class ApplyTenantRuleSetCommandHandler(
    IRuleSetRepository ruleSets,
    ITenantRuleSetApplicationRepository applications)
    : ICommandHandler<ApplyTenantRuleSetCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ApplyTenantRuleSetCommand command, CancellationToken cancellationToken)
    {
        var ruleSet = command.RuleSetId is { } id
            ? await ruleSets.GetAsync(id, cancellationToken)
            : await ruleSets.LatestAsync(CaMbRuleSets.Jurisdiction, cancellationToken);

        if (ruleSet is null)
        {
            return Result.Failure<Guid>(command.RuleSetId is null ? RuleSetErrors.NoPublishedRuleSet : RuleSetErrors.NotFound);
        }

        var application = TenantRuleSetApplication.Apply(command.TenantId, ruleSet, command.ChangeNotice, command.ActorId, DateTimeOffset.UtcNow);
        if (application.IsFailure)
        {
            return Result.Failure<Guid>(application.Error);
        }

        applications.Add(application.Value);
        await applications.SaveChangesAsync(cancellationToken);
        return Result.Success(application.Value.Id);
    }
}
