using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.RuleSets.Events;

/// <summary>A tenant adopted a published rule-set version.</summary>
public sealed record RuleSetAppliedDomainEvent(
    Guid ApplicationId,
    Guid TenantId,
    Guid RuleSetId,
    string Jurisdiction,
    int RuleSetVersion,
    string? ChangeNotice,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
