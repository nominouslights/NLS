using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Tests;

/// <summary>Shared builders for Budgeting tests.</summary>
internal static class TestBudgeting
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>A stand-in for an authenticated user id, for the created_by/modified_by paths.</summary>
    public static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// A valid set of code details, with every field overridable per test. Defaults are the
    /// required-only shape plus a service line, so a test that cares about one field does not
    /// have to restate the rest.
    /// </summary>
    public static BudgetCodeDetails CodeDetails(
        string name = "Alamos crew shuttle",
        BudgetCodeCategory category = BudgetCodeCategory.Revenue,
        BudgetReviewFrequency reviewFrequency = BudgetReviewFrequency.Quarterly,
        BudgetServiceLine? serviceLine = BudgetServiceLine.ContractCrew,
        string? costCentre = null,
        Guid? parentCodeId = null,
        string? glAccountCode = null,
        BudgetTaxTreatment? taxTreatment = null,
        Guid? budgetOwnerUserId = null,
        string? description = "Contracted crew rotation runs under the Alamos master agreement.") => new()
        {
            Name = name,
            Category = category,
            ReviewFrequency = reviewFrequency,
            ServiceLine = serviceLine,
            CostCentre = costCentre,
            ParentCodeId = parentCodeId,
            GlAccountCode = glAccountCode,
            TaxTreatment = taxTreatment,
            BudgetOwnerUserId = budgetOwnerUserId,
            Description = description,
        };

    public static BudgetCode CreateCode(
        string code = "ZBB-CREW-01",
        BudgetCodeDetails? details = null,
        Guid? actorId = null)
    {
        var result = BudgetCode.Create(TenantId, code, details ?? CodeDetails(), actorId);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test budget code invalid: {result.Error.Code}");
        }

        return result.Value;
    }

    public static BudgetPeriod CreatePeriod(
        PeriodGranularity granularity = PeriodGranularity.Quarter,
        int year = 2026,
        int ordinal = 4)
    {
        var result = BudgetPeriod.Create(TenantId, granularity, year, ordinal);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test period invalid: {result.Error.Code}");
        }

        return result.Value;
    }
}
