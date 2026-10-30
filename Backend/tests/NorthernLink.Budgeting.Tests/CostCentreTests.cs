using NorthernLink.Budgeting.Domain.CostCentres;
using NorthernLink.Budgeting.Domain.CostCentres.Events;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>The CostCentre aggregate: normalization, immutability, validation, events.</summary>
public class CostCentreTests
{
    private static CostCentreDetails Details(
        string name = "Thompson base",
        string? description = null,
        Guid? ownerUserId = null,
        Guid? parentId = null) => new()
        {
            Name = name,
            Description = description,
            OwnerUserId = ownerUserId,
            ParentId = parentId,
        };

    [Fact]
    public void Create_produces_an_active_entry_and_raises_created()
    {
        var result = CostCentre.Create(TestBudgeting.TenantId, "THOMPSON", Details(), TestBudgeting.ActorId);

        Assert.True(result.IsSuccess);
        var costCentre = result.Value;
        Assert.True(costCentre.IsActive);
        Assert.Equal(TestBudgeting.TenantId, costCentre.TenantId);
        Assert.Equal(TestBudgeting.ActorId, costCentre.CreatedBy);
        var created = Assert.IsType<CostCentreCreatedDomainEvent>(Assert.Single(costCentre.DomainEvents));
        Assert.Equal("THOMPSON", created.Code);
        Assert.Equal(costCentre.Id, created.CostCentreId);
    }

    [Fact]
    public void The_code_is_trimmed_and_its_case_preserved()
    {
        // Mirrors how BudgetCode has always stored its cost-centre string (trim, nothing else), so
        // an existing "Ops-North" on a budget code matches its backfilled register entry.
        var costCentre = CostCentre.Create(TestBudgeting.TenantId, "  Ops-North ", Details(), null).Value;

        Assert.Equal("Ops-North", costCentre.Code);
        Assert.Equal("Ops-North", CostCentre.NormalizeCode("  Ops-North "));
        Assert.Equal(string.Empty, CostCentre.NormalizeCode(null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_code_is_rejected(string? code)
    {
        var result = CostCentre.Create(TestBudgeting.TenantId, code, Details(), null);

        Assert.Equal(CostCentreErrors.CodeRequired, result.Error);
    }

    [Fact]
    public void The_code_length_boundary_is_32_characters_after_trimming()
    {
        Assert.True(CostCentre.Create(TestBudgeting.TenantId, " " + new string('A', 32) + " ", Details(), null).IsSuccess);
        Assert.Equal(
            CostCentreErrors.CodeTooLong,
            CostCentre.Create(TestBudgeting.TenantId, new string('A', 33), Details(), null).Error);
    }

    [Fact]
    public void Name_is_required_and_bounded()
    {
        Assert.Equal(
            CostCentreErrors.NameRequired,
            CostCentre.Create(TestBudgeting.TenantId, "X", Details(name: "  "), null).Error);
        Assert.Equal(
            CostCentreErrors.NameTooLong,
            CostCentre.Create(TestBudgeting.TenantId, "X", Details(name: new string('n', 121)), null).Error);
        Assert.True(CostCentre.Create(TestBudgeting.TenantId, "X", Details(name: new string('n', 120)), null).IsSuccess);
    }

    [Fact]
    public void Description_is_optional_bounded_and_blank_is_null()
    {
        Assert.Null(CostCentre.Create(TestBudgeting.TenantId, "X", Details(description: "   "), null).Value.Description);
        Assert.Equal(
            CostCentreErrors.DescriptionTooLong,
            CostCentre.Create(TestBudgeting.TenantId, "X", Details(description: new string('d', 1001)), null).Error);
    }

    [Fact]
    public void Update_rewrites_details_but_never_the_code_and_raises_updated()
    {
        var costCentre = TestBudgeting.CreateCostCentre("THOMPSON", "Thompson base");
        var owner = Guid.NewGuid();

        var result = costCentre.Update(Details(name: " Thompson yard ", ownerUserId: owner), TestBudgeting.ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("THOMPSON", costCentre.Code);
        Assert.Equal("Thompson yard", costCentre.Name);
        Assert.Equal(owner, costCentre.OwnerUserId);
        Assert.Equal(TestBudgeting.ActorId, costCentre.ModifiedBy);
        Assert.IsType<CostCentreUpdatedDomainEvent>(Assert.Single(costCentre.DomainEvents));
    }

    [Fact]
    public void An_update_that_changes_nothing_raises_no_event()
    {
        var costCentre = TestBudgeting.CreateCostCentre("THOMPSON", "Thompson base", description: "Yard");

        var result = costCentre.Update(Details(name: "  Thompson base", description: "Yard  "), TestBudgeting.ActorId);

        Assert.True(result.IsSuccess);
        Assert.Empty(costCentre.DomainEvents);
        Assert.Null(costCentre.ModifiedBy);
    }

    [Fact]
    public void A_cost_centre_cannot_be_its_own_parent()
    {
        var costCentre = TestBudgeting.CreateCostCentre();

        var result = costCentre.Update(Details(parentId: costCentre.Id), null);

        Assert.Equal(CostCentreErrors.ParentIsSelf, result.Error);
        Assert.Null(costCentre.ParentId);
    }

    [Fact]
    public void SetActive_flips_once_and_is_a_silent_no_op_when_unchanged()
    {
        var costCentre = TestBudgeting.CreateCostCentre();

        Assert.True(costCentre.SetActive(true, null).IsSuccess);
        Assert.Empty(costCentre.DomainEvents);

        Assert.True(costCentre.SetActive(false, TestBudgeting.ActorId).IsSuccess);
        Assert.False(costCentre.IsActive);
        var changed = Assert.IsType<CostCentreActivationChangedDomainEvent>(Assert.Single(costCentre.DomainEvents));
        Assert.False(changed.IsActive);

        Assert.True(costCentre.SetActive(false, TestBudgeting.ActorId).IsSuccess);
        Assert.Single(costCentre.DomainEvents);
    }
}
