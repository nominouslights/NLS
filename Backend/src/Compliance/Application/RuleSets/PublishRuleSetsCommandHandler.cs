using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.RuleSets;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.RuleSets;

/// <summary>
/// Handles <see cref="PublishRuleSetsCommand"/> — NL-SP-02 s. 13 (the plan's own rules are
/// versioned and reviewed): a published row records exactly which numbers were in force when.
/// </summary>
public sealed class PublishRuleSetsCommandHandler(IRuleSetRepository ruleSets)
    : ICommandHandler<PublishRuleSetsCommand, RuleSetPublishResult>
{
    public async Task<Result<RuleSetPublishResult>> Handle(PublishRuleSetsCommand command, CancellationToken cancellationToken)
    {
        var published = new List<RuleSetPublishedResponse>();
        foreach (var document in CaMbRuleSets.All)
        {
            var existing = await ruleSets.FindAsync(document.Jurisdiction, document.Version, cancellationToken);
            if (existing is not null)
            {
                published.Add(new RuleSetPublishedResponse(existing.Id, existing.Jurisdiction, existing.Version, Created: false));
                continue;
            }

            var ruleSet = RuleSet.Publish(document, DateTimeOffset.UtcNow);
            await ruleSets.AddAsync(ruleSet, cancellationToken);
            published.Add(new RuleSetPublishedResponse(ruleSet.Id, ruleSet.Jurisdiction, ruleSet.Version, Created: true));
        }

        return Result.Success(new RuleSetPublishResult(published));
    }
}
