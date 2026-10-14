using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Compliance.Domain.RuleSets.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.RuleSets;

/// <summary>
/// The tenant's adoption of a published <see cref="RuleSet"/> version from a moment on. The
/// newest application whose <see cref="AppliedAtUtc"/> is at or before an as-of date is the
/// rule set in force for that date; with no application at all, the in-code CA-MB default
/// applies (<c>GetActiveRules</c> says which). Tenant-scoped, audited, append-only.
/// </summary>
public sealed class TenantRuleSetApplication : AggregateRoot, ITenantScoped
{
    public const int ChangeNoticeMaxLength = 2000;

    private TenantRuleSetApplication()
    {
        // EF Core materialization only.
    }

    public Guid TenantId { get; private set; }
    public Guid RuleSetId { get; private set; }
    public DateTimeOffset AppliedAtUtc { get; private set; }

    /// <summary>What changed and why — printed in the next Monthly Compliance Report.</summary>
    public string? ChangeNotice { get; private set; }

    public Guid? AppliedBy { get; private set; }

    public static Result<TenantRuleSetApplication> Apply(Guid tenantId, RuleSet ruleSet, string? changeNotice, Guid? actorId, DateTimeOffset now)
    {
        var notice = string.IsNullOrWhiteSpace(changeNotice) ? null : changeNotice.Trim();
        if (notice?.Length > ChangeNoticeMaxLength)
        {
            return Result.Failure<TenantRuleSetApplication>(RuleSetErrors.ChangeNoticeTooLong);
        }

        var application = new TenantRuleSetApplication
        {
            TenantId = tenantId,
            RuleSetId = ruleSet.Id,
            AppliedAtUtc = now,
            ChangeNotice = notice,
            AppliedBy = actorId,
        };
        application.Raise(new RuleSetAppliedDomainEvent(
            application.Id, tenantId, ruleSet.Id, ruleSet.Jurisdiction, ruleSet.Version, notice, actorId));
        return Result.Success(application);
    }
}

public static class RuleSetErrors
{
    public static readonly Error NotFound = ComplianceError.NotFound(
        "Compliance.RuleSet.NotFound", "No published rule set with that id exists.",
        BasisKeys.Plan13);

    public static readonly Error NoPublishedRuleSet = ComplianceError.Conflict(
        "Compliance.RuleSet.NoPublishedRuleSet",
        "No CA-MB rule set has been published yet. POST /api/compliance/rule-sets/publish first.",
        BasisKeys.Plan13);

    public static readonly Error ChangeNoticeTooLong = ComplianceError.Validation(
        "Compliance.RuleSet.ChangeNoticeTooLong",
        $"The change notice must be {TenantRuleSetApplication.ChangeNoticeMaxLength} characters or fewer.",
        BasisKeys.Plan13);

    public static readonly Error RuleKeyUnknown = ComplianceError.NotFound(
        "Compliance.RuleSet.RuleKeyUnknown", "No rule exists under that key in the rule set in force.",
        BasisKeys.Plan13);

    public static readonly Error RuleKeyRequired = ComplianceError.Validation(
        "Compliance.RuleSet.RuleKeyRequired", "A rule key is required (for example hos.dailyDrivingSeconds).",
        BasisKeys.Plan13);
}
