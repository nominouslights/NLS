using NorthernLink.Compliance.Application.RegulatoryBasis;
using NorthernLink.Compliance.Application.RuleSets;
using NorthernLink.Compliance.Application.Settings;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.RuleSets;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>Publishing, applying and resolving rule sets, and the idempotent seed.</summary>
public class RuleSetAndSeedHandlerTests
{
    private readonly InMemoryReferenceStore _reference = new();
    private readonly InMemoryRuleSetApplicationRepository _applications = new();
    private readonly InMemoryComplianceStore _store = new();

    [Fact]
    public async Task Publishing_is_idempotent_by_jurisdiction_and_version()
    {
        var handler = new PublishRuleSetsCommandHandler(_reference);

        var first = await handler.Handle(new PublishRuleSetsCommand(TestCompliance.ActorId), CancellationToken.None);
        var second = await handler.Handle(new PublishRuleSetsCommand(TestCompliance.ActorId), CancellationToken.None);

        Assert.True(Assert.Single(first.Value.RuleSets).Created);
        Assert.False(Assert.Single(second.Value.RuleSets).Created);
        Assert.Equal(first.Value.RuleSets[0].Id, second.Value.RuleSets[0].Id);
        var published = Assert.Single(_reference.RuleSets);
        Assert.Equal("CA-MB", published.Jurisdiction);
        Assert.Equal(1, published.Version);
    }

    [Fact]
    public async Task Applying_without_a_published_set_is_a_readable_conflict()
    {
        var handler = new ApplyTenantRuleSetCommandHandler(_reference, _applications);

        var result = await handler.Handle(new ApplyTenantRuleSetCommand(TestCompliance.TenantId, null, null, null), CancellationToken.None);

        Assert.Equal("Compliance.RuleSet.NoPublishedRuleSet", result.Error.Code);
    }

    [Fact]
    public async Task An_applied_set_answers_as_Applied_and_an_earlier_as_of_date_falls_back_to_the_default()
    {
        await new PublishRuleSetsCommandHandler(_reference).Handle(new PublishRuleSetsCommand(null), CancellationToken.None);
        var apply = await new ApplyTenantRuleSetCommandHandler(_reference, _applications)
            .Handle(new ApplyTenantRuleSetCommand(TestCompliance.TenantId, null, "Adopted at go-live", TestCompliance.ActorId), CancellationToken.None);
        Assert.True(apply.IsSuccess);

        var rules = new GetActiveRulesQueryHandler(new ActiveRuleSetResolver(_reference, _applications));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var applied = await rules.Handle(new GetActiveRulesQuery(TestCompliance.TenantId, today), CancellationToken.None);
        var beforeApplication = await rules.Handle(new GetActiveRulesQuery(TestCompliance.TenantId, today.AddYears(-1)), CancellationToken.None);

        Assert.Equal(RuleSource.Applied, applied.Value.Source);
        Assert.Equal(apply.Value, applied.Value.ApplicationId);
        Assert.Equal("Adopted at go-live", applied.Value.ChangeNotice);
        Assert.Equal(CaMbRuleSets.Current.Flatten(), applied.Value.Values);

        Assert.Equal(RuleSource.Default, beforeApplication.Value.Source);
        Assert.Null(beforeApplication.Value.RuleSetId);
    }

    [Fact]
    public async Task Another_tenants_application_is_invisible()
    {
        await new PublishRuleSetsCommandHandler(_reference).Handle(new PublishRuleSetsCommand(null), CancellationToken.None);
        await new ApplyTenantRuleSetCommandHandler(_reference, _applications)
            .Handle(new ApplyTenantRuleSetCommand(TestCompliance.TenantId, null, null, null), CancellationToken.None);
        _applications.QueryFilterTenantId = Guid.NewGuid();

        var rules = await new GetActiveRulesQueryHandler(new ActiveRuleSetResolver(_reference, _applications))
            .Handle(new GetActiveRulesQuery(_applications.QueryFilterTenantId, DateOnly.FromDateTime(DateTime.UtcNow)), CancellationToken.None);

        Assert.Equal(RuleSource.Default, rules.Value.Source);
    }

    [Fact]
    public async Task Seed_defaults_creates_everything_once_and_nothing_twice()
    {
        var handler = new SeedComplianceDefaultsCommandHandler(_store, _store, _store, _reference, _applications, _reference);

        var first = await handler.Handle(new SeedComplianceDefaultsCommand(TestCompliance.TenantId, TestCompliance.ActorId), CancellationToken.None);
        var second = await handler.Handle(new SeedComplianceDefaultsCommand(TestCompliance.TenantId, TestCompliance.ActorId), CancellationToken.None);

        Assert.True(first.IsSuccess, first.IsFailure ? first.Error.Code : null);
        Assert.True(first.Value.SettingsCreated);
        Assert.True(first.Value.RuleSetPublished);
        Assert.True(first.Value.RuleSetApplied);
        Assert.Equal(Enum.GetValues<DocumentSeriesCode>().Length, first.Value.SeriesCreated);
        Assert.Equal(RegulatoryBasisSeed.All.Count, first.Value.RegulatoryBasisInserted);

        Assert.Equal(new SeedDefaultsResult(false, false, false, 0, 0), second.Value);

        var settings = Assert.Single(_store.Settings);
        Assert.Equal("NL", settings.DocumentPrefix);
        Assert.Single(_reference.RuleSets);
        Assert.Single(_applications.Applications);
        Assert.Equal(RegulatoryBasisSeed.All.Count, _reference.Basis.Count);
    }

    [Fact]
    public async Task The_basis_lookup_returns_entries_in_the_requested_order_and_refuses_unknown_keys()
    {
        await _reference.UpsertAsync(RegulatoryBasisSeed.All);
        var handler = new GetRegulatoryBasisQueryHandler(_reference);

        var found = await handler.Handle(new GetRegulatoryBasisQuery(["NSC.13.16", "HTA.318.8.1"]), CancellationToken.None);
        var unknown = await handler.Handle(new GetRegulatoryBasisQuery(["NSC.13.16", "HTA.999"]), CancellationToken.None);
        var empty = await handler.Handle(new GetRegulatoryBasisQuery([]), CancellationToken.None);

        Assert.Equal(["NSC.13.16", "HTA.318.8.1"], found.Value.Select(b => b.Key));
        Assert.Equal("Compliance.RegulatoryBasis.NotFound", unknown.Error.Code);
        Assert.Equal("Compliance.RegulatoryBasis.KeysRequired", empty.Error.Code);
    }

    [Fact]
    public async Task Settings_update_creates_when_absent_and_validates_the_prefix()
    {
        var handler = new UpdateComplianceSettingsCommandHandler(_store, _store);

        var bad = await handler.Handle(new UpdateComplianceSettingsCommand(TestCompliance.TenantId, new ComplianceSettingsDetails
        {
            CarrierLegalName = "Northern Link Shuttle & Cargo", DocumentPrefix = "N-L", PrincipalPlaceOfBusiness = "Leaf Rapids, Manitoba",
        }, null), CancellationToken.None);
        var good = await handler.Handle(new UpdateComplianceSettingsCommand(TestCompliance.TenantId, new ComplianceSettingsDetails
        {
            CarrierLegalName = "Northern Link Shuttle & Cargo", DocumentPrefix = "nl", PrincipalPlaceOfBusiness = "Leaf Rapids, Manitoba", NscNumber = "MB-12345",
        }, TestCompliance.ActorId), CancellationToken.None);

        Assert.Equal("Compliance.Settings.DocumentPrefixInvalid", bad.Error.Code);
        Assert.True(good.IsSuccess);
        var settings = Assert.Single(_store.Settings);
        Assert.Equal("NL", settings.DocumentPrefix);
        Assert.Equal("MB-12345", settings.NscNumber);
    }
}
