using NorthernLink.Budgeting.Application.Vendors;
using NorthernLink.Budgeting.Application.Vendors.Create;
using NorthernLink.Budgeting.Application.Vendors.Delete;
using NorthernLink.Budgeting.Application.Vendors.SetActive;
using NorthernLink.Budgeting.Application.Vendors.Update;
using NorthernLink.Budgeting.Domain.Vendors;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>The vendor write handlers: case-insensitive uniqueness, NotFound, retire, delete guard.</summary>
public class VendorHandlerTests
{
    private readonly InMemoryVendorRepository _repository = new();
    private readonly StubVendorUsageProbe _usage = new();
    private readonly CreateVendorCommandHandler _create;
    private readonly UpdateVendorCommandHandler _update;
    private readonly SetVendorActiveCommandHandler _setActive;
    private readonly DeleteVendorCommandHandler _delete;

    public VendorHandlerTests()
    {
        _create = new CreateVendorCommandHandler(_repository);
        _update = new UpdateVendorCommandHandler(_repository);
        _setActive = new SetVendorActiveCommandHandler(_repository);
        _delete = new DeleteVendorCommandHandler(_repository, _usage);
    }

    private static VendorDetails Named(string? name, string? email = null) => new() { Name = name, Email = email };

    private Task<Result<Guid>> CreateAsync(string? name, string? email = null) =>
        _create.Handle(
            new CreateVendorCommand(TestBudgeting.TenantId, Named(name, email), TestBudgeting.ActorId),
            CancellationToken.None);

    private Task<Result> UpdateAsync(Guid vendorId, string? name, string? email = null) =>
        _update.Handle(
            new UpdateVendorCommand(TestBudgeting.TenantId, vendorId, Named(name, email), TestBudgeting.ActorId),
            CancellationToken.None);

    private async Task<Vendor> SeedAsync(string name)
    {
        var result = await CreateAsync(name);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        var vendor = _repository.Vendors.Single(v => v.Id == result.Value);
        vendor.ClearDomainEvents();
        return vendor;
    }

    // --- Create ----------------------------------------------------------------------------------

    [Fact]
    public async Task Creates_a_vendor_and_saves_once()
    {
        var result = await CreateAsync("Acme Fuel");

        Assert.True(result.IsSuccess);
        var vendor = Assert.Single(_repository.Vendors);
        Assert.Equal(result.Value, vendor.Id);
        Assert.Equal(TestBudgeting.ActorId, vendor.CreatedBy);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Theory]
    [InlineData("Acme Fuel")]
    [InlineData("ACME FUEL")]
    [InlineData("  acme fuel  ")]
    public async Task A_name_already_taken_ignoring_case_is_a_409_naming_the_existing_vendor(string attempt)
    {
        await SeedAsync("Acme Fuel");

        var result = await CreateAsync(attempt);

        Assert.True(result.IsFailure);
        Assert.Equal("Budgeting.Vendor.DuplicateName", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Contains("\"Acme Fuel\"", result.Error.Message);
        Assert.Single(_repository.Vendors);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_retired_vendor_still_holds_its_name_and_the_message_says_to_reactivate()
    {
        var retired = await SeedAsync("Acme Fuel");
        retired.SetActive(false, null);

        var result = await CreateAsync("acme fuel");

        Assert.Equal("Budgeting.Vendor.DuplicateName", result.Error.Code);
        Assert.Contains("Reactivate", result.Error.Message);
    }

    [Fact]
    public async Task Another_tenants_vendor_does_not_collide()
    {
        await SeedAsync("Acme Fuel");
        _repository.QueryFilterTenantId = Guid.NewGuid();

        var result = await _create.Handle(
            new CreateVendorCommand(_repository.QueryFilterTenantId, Named("Acme Fuel"), null), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Invalid_details_report_validation_not_conflict()
    {
        await SeedAsync("Acme Fuel");

        var result = await CreateAsync("Acme Fuel", email: "not an email");

        Assert.Equal("Budgeting.Vendor.EmailInvalid", result.Error.Code);
    }

    // --- Update / rename -------------------------------------------------------------------------

    [Fact]
    public async Task Renaming_onto_another_vendors_name_ignoring_case_is_a_409()
    {
        await SeedAsync("Acme Fuel");
        var other = await SeedAsync("Borealis Tire");

        var result = await UpdateAsync(other.Id, "ACME fuel");

        Assert.Equal("Budgeting.Vendor.DuplicateName", result.Error.Code);
        Assert.Equal("Borealis Tire", other.Name);
        Assert.Empty(other.DomainEvents);
        Assert.Equal(2, _repository.SaveChangesCallCount); // the two seeds only
    }

    [Fact]
    public async Task Renaming_to_its_own_name_in_a_different_case_is_allowed()
    {
        var vendor = await SeedAsync("Acme fuel");

        var result = await UpdateAsync(vendor.Id, "ACME Fuel");

        Assert.True(result.IsSuccess);
        Assert.Equal("ACME Fuel", vendor.Name);
        Assert.Equal(TestBudgeting.ActorId, vendor.ModifiedBy);
    }

    [Fact]
    public async Task Renaming_to_a_free_name_succeeds_and_saves()
    {
        var vendor = await SeedAsync("Acme Fuel");

        var result = await UpdateAsync(vendor.Id, "Acme Energy");

        Assert.True(result.IsSuccess);
        Assert.Equal("ACME ENERGY", vendor.NormalizedName);
        Assert.Equal(2, _repository.SaveChangesCallCount);
    }

    // --- Unique-index race (the pre-check passed, the commit lost) -------------------------------

    [Fact]
    public async Task A_create_that_loses_the_unique_index_race_is_a_409_naming_the_winner()
    {
        var winner = Vendor.Create(TestBudgeting.TenantId, Named("ACME Fuel"), null).Value;
        _repository.UniqueNameViolationOnNextSave = true;
        _repository.RaceWinner = winner;

        var result = await CreateAsync("acme fuel");

        Assert.True(result.IsFailure);
        Assert.Equal("Budgeting.Vendor.DuplicateName", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Contains("\"ACME Fuel\"", result.Error.Message);
        Assert.Equal(winner.Id, Assert.Single(_repository.Vendors).Id); // the loser persisted nothing
    }

    [Fact]
    public async Task A_create_that_loses_the_race_to_an_unreadable_winner_is_a_generic_409()
    {
        _repository.UniqueNameViolationOnNextSave = true;

        var result = await CreateAsync("Acme Fuel");

        Assert.Equal("Budgeting.Vendor.DuplicateName", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(VendorErrors.DuplicateNameTaken.Message, result.Error.Message);
        Assert.Empty(_repository.Vendors);
    }

    [Fact]
    public async Task A_rename_that_loses_the_unique_index_race_is_a_409_naming_the_winner()
    {
        var vendor = await SeedAsync("Borealis Tire");
        var winner = Vendor.Create(TestBudgeting.TenantId, Named("Acme Fuel"), null).Value;
        _repository.UniqueNameViolationOnNextSave = true;
        _repository.RaceWinner = winner;

        var result = await UpdateAsync(vendor.Id, "ACME FUEL");

        Assert.True(result.IsFailure);
        Assert.Equal("Budgeting.Vendor.DuplicateName", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Contains("\"Acme Fuel\"", result.Error.Message);
    }

    [Fact]
    public async Task A_rename_that_loses_the_race_to_an_unreadable_winner_is_a_generic_409()
    {
        var vendor = await SeedAsync("Borealis Tire");
        _repository.UniqueNameViolationOnNextSave = true;

        // The only row now matching the name is the loser's own (unsaved) rename — never itself a conflict.
        var result = await UpdateAsync(vendor.Id, "Acme Fuel");

        Assert.Equal("Budgeting.Vendor.DuplicateName", result.Error.Code);
        Assert.Equal(VendorErrors.DuplicateNameTaken.Message, result.Error.Message);
    }

    [Fact]
    public async Task Updating_a_missing_vendor_is_NotFound()
    {
        var result = await UpdateAsync(Guid.NewGuid(), "Acme Fuel");

        Assert.Equal("Budgeting.Vendor.NotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Another_tenants_vendor_reads_as_NotFound()
    {
        var vendor = await SeedAsync("Acme Fuel");
        _repository.QueryFilterTenantId = Guid.NewGuid();

        var update = await UpdateAsync(vendor.Id, "Hijacked");
        var retire = await _setActive.Handle(
            new SetVendorActiveCommand(_repository.QueryFilterTenantId, vendor.Id, false, null), CancellationToken.None);
        var delete = await _delete.Handle(
            new DeleteVendorCommand(_repository.QueryFilterTenantId, vendor.Id), CancellationToken.None);

        Assert.Equal("Budgeting.Vendor.NotFound", update.Error.Code);
        Assert.Equal("Budgeting.Vendor.NotFound", retire.Error.Code);
        Assert.Equal("Budgeting.Vendor.NotFound", delete.Error.Code);
        Assert.Single(_repository.Vendors);
        Assert.True(vendor.IsActive);
    }

    [Fact]
    public async Task An_invalid_update_is_validation_even_for_a_missing_vendor()
    {
        var result = await UpdateAsync(Guid.NewGuid(), "  ");

        Assert.Equal("Budgeting.Vendor.NameRequired", result.Error.Code);
    }

    // --- Activate / deactivate -------------------------------------------------------------------

    [Fact]
    public async Task Deactivate_and_activate_flip_the_flag()
    {
        var vendor = await SeedAsync("Acme Fuel");

        var retire = await _setActive.Handle(
            new SetVendorActiveCommand(TestBudgeting.TenantId, vendor.Id, false, TestBudgeting.ActorId), CancellationToken.None);
        Assert.True(retire.IsSuccess);
        Assert.False(vendor.IsActive);

        var restore = await _setActive.Handle(
            new SetVendorActiveCommand(TestBudgeting.TenantId, vendor.Id, true, TestBudgeting.ActorId), CancellationToken.None);
        Assert.True(restore.IsSuccess);
        Assert.True(vendor.IsActive);
    }

    [Fact]
    public async Task Activating_a_missing_vendor_is_NotFound()
    {
        var result = await _setActive.Handle(
            new SetVendorActiveCommand(TestBudgeting.TenantId, Guid.NewGuid(), true, null), CancellationToken.None);

        Assert.Equal("Budgeting.Vendor.NotFound", result.Error.Code);
    }

    // --- Delete ----------------------------------------------------------------------------------

    [Fact]
    public async Task An_unreferenced_vendor_is_deleted()
    {
        var vendor = await SeedAsync("Acme Fuel");

        var result = await _delete.Handle(
            new DeleteVendorCommand(TestBudgeting.TenantId, vendor.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_repository.Vendors);
        Assert.Equal(vendor.Id, _usage.LastProbedId);
        Assert.Equal(2, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_referenced_vendor_is_InUse_and_nothing_is_removed()
    {
        var vendor = await SeedAsync("Acme Fuel");
        _usage.Referenced = true;

        var result = await _delete.Handle(
            new DeleteVendorCommand(TestBudgeting.TenantId, vendor.Id), CancellationToken.None);

        Assert.Equal("Budgeting.Vendor.InUse", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Single(_repository.Vendors);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Deleting_a_missing_vendor_is_NotFound()
    {
        var result = await _delete.Handle(
            new DeleteVendorCommand(TestBudgeting.TenantId, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("Budgeting.Vendor.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task The_shipped_usage_probe_reports_no_vendor_in_use_until_items_reference_vendors()
    {
        var probe = new UnreferencedVendorUsageProbe();

        Assert.False(await probe.IsReferencedAsync(Guid.NewGuid()));
    }
}
