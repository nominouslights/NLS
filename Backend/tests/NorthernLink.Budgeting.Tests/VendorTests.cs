using NorthernLink.Budgeting.Domain.Vendors;
using NorthernLink.Budgeting.Domain.Vendors.Events;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>The Vendor aggregate's own rules: name, trimming, field caps, GST length-only, events.</summary>
public class VendorTests
{
    private static VendorDetails Details(
        string? name = "Acme Fuel Ltd.",
        string? contactName = null,
        string? email = null,
        string? phone = null,
        string? address = null,
        string? notes = null,
        string? gstRegistrationNumber = null,
        string? qboDisplayName = null,
        string? defaultBudgetCode = null) => new()
        {
            Name = name,
            ContactName = contactName,
            Email = email,
            Phone = phone,
            Address = address,
            Notes = notes,
            GstRegistrationNumber = gstRegistrationNumber,
            QboDisplayName = qboDisplayName,
            DefaultBudgetCode = defaultBudgetCode,
        };

    private static Vendor Create(VendorDetails? details = null)
    {
        var result = Vendor.Create(TestBudgeting.TenantId, details ?? Details(), TestBudgeting.ActorId);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        result.Value.ClearDomainEvents();
        return result.Value;
    }

    // --- Create ----------------------------------------------------------------------------------

    [Fact]
    public void Create_makes_an_active_vendor_stamped_with_tenant_and_actor_and_raises_created()
    {
        var result = Vendor.Create(TestBudgeting.TenantId, Details(), TestBudgeting.ActorId);

        Assert.True(result.IsSuccess);
        var vendor = result.Value;
        Assert.Equal(TestBudgeting.TenantId, vendor.TenantId);
        Assert.True(vendor.IsActive);
        Assert.Equal(TestBudgeting.ActorId, vendor.CreatedBy);
        Assert.Null(vendor.ModifiedBy);
        Assert.Equal(vendor.CreatedAtUtc, vendor.UpdatedAtUtc);

        var created = Assert.IsType<VendorCreatedDomainEvent>(Assert.Single(vendor.DomainEvents));
        Assert.Equal(vendor.Id, created.VendorId);
        Assert.Equal(TestBudgeting.TenantId, created.TenantId);
        Assert.Equal("Acme Fuel Ltd.", created.Name);
        Assert.Equal(TestBudgeting.ActorId, created.ActorId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_name_is_NameRequired(string? name)
    {
        var result = Vendor.Create(TestBudgeting.TenantId, Details(name: name), null);

        Assert.True(result.IsFailure);
        Assert.Equal("Budgeting.Vendor.NameRequired", result.Error.Code);
    }

    [Fact]
    public void The_name_cap_matches_the_free_text_vendor_on_a_budget_item()
    {
        Assert.Equal(Domain.Allocations.BudgetAllocation.VendorMaxLength, Vendor.NameMaxLength);
    }

    [Fact]
    public void A_name_over_the_cap_is_NameTooLong_but_exactly_the_cap_after_trim_is_fine()
    {
        var atCap = Vendor.Create(
            TestBudgeting.TenantId, Details(name: "  " + new string('a', Vendor.NameMaxLength) + "  "), null);
        var overCap = Vendor.Create(
            TestBudgeting.TenantId, Details(name: new string('a', Vendor.NameMaxLength + 1)), null);

        Assert.True(atCap.IsSuccess);
        Assert.Equal("Budgeting.Vendor.NameTooLong", overCap.Error.Code);
    }

    [Fact]
    public void Every_string_is_trimmed_blank_optionals_become_null_and_the_name_is_normalized()
    {
        var vendor = Create(Details(
            name: "  Acme Fuel Ltd.  ",
            contactName: "  Pat  ",
            email: "  ap@acme.ca ",
            phone: "   ",
            address: "",
            notes: null,
            gstRegistrationNumber: "  123456789 RT0001 ",
            qboDisplayName: "  ACME FUEL  ",
            defaultBudgetCode: "  fleet-fuel "));

        Assert.Equal("Acme Fuel Ltd.", vendor.Name);
        Assert.Equal("ACME FUEL LTD.", vendor.NormalizedName);
        Assert.Equal("Pat", vendor.ContactName);
        Assert.Equal("ap@acme.ca", vendor.Email);
        Assert.Null(vendor.Phone);
        Assert.Null(vendor.Address);
        Assert.Null(vendor.Notes);
        Assert.Equal("123456789 RT0001", vendor.GstRegistrationNumber);
        Assert.Equal("ACME FUEL", vendor.QboDisplayName);
        Assert.Equal("FLEET-FUEL", vendor.DefaultBudgetCode);
    }

    [Theory]
    [InlineData("anything at all, not a BN")]
    [InlineData("123")]
    [InlineData("RT0001")]
    public void The_GST_registration_number_is_checked_for_length_only(string value)
    {
        var vendor = Create(Details(gstRegistrationNumber: value));

        Assert.Equal(value, vendor.GstRegistrationNumber);
    }

    [Fact]
    public void A_GST_registration_number_over_32_is_GstRegistrationNumberTooLong()
    {
        var atCap = Vendor.Create(
            TestBudgeting.TenantId, Details(gstRegistrationNumber: new string('1', Vendor.GstRegistrationNumberMaxLength)), null);
        var overCap = Vendor.Create(
            TestBudgeting.TenantId, Details(gstRegistrationNumber: new string('1', Vendor.GstRegistrationNumberMaxLength + 1)), null);

        Assert.Equal(32, Vendor.GstRegistrationNumberMaxLength);
        Assert.True(atCap.IsSuccess);
        Assert.Equal("Budgeting.Vendor.GstRegistrationNumberTooLong", overCap.Error.Code);
    }

    public static TheoryData<VendorDetails, string> OverCapFields => new()
    {
        { Details(contactName: new string('a', Vendor.ContactNameMaxLength + 1)), "Budgeting.Vendor.ContactNameTooLong" },
        { Details(email: new string('a', Vendor.EmailMaxLength) + "@x.ca"), "Budgeting.Vendor.EmailTooLong" },
        { Details(phone: new string('1', Vendor.PhoneMaxLength + 1)), "Budgeting.Vendor.PhoneTooLong" },
        { Details(address: new string('a', Vendor.AddressMaxLength + 1)), "Budgeting.Vendor.AddressTooLong" },
        { Details(notes: new string('a', Vendor.NotesMaxLength + 1)), "Budgeting.Vendor.NotesTooLong" },
        { Details(qboDisplayName: new string('a', Vendor.QboDisplayNameMaxLength + 1)), "Budgeting.Vendor.QboDisplayNameTooLong" },
        { Details(defaultBudgetCode: new string('A', Vendor.DefaultBudgetCodeMaxLength + 1)), "Budgeting.Vendor.DefaultBudgetCodeTooLong" },
    };

    [Theory]
    [MemberData(nameof(OverCapFields))]
    public void Each_optional_field_has_its_own_length_error(VendorDetails details, string expectedCode)
    {
        var result = Vendor.Create(TestBudgeting.TenantId, details, null);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
    }

    [Theory]
    [InlineData("not an email")]
    [InlineData("@acme.ca")]
    [InlineData("ap@")]
    [InlineData("ap@@acme.ca")]
    [InlineData("807 555 0100")]
    public void An_email_without_an_address_shape_is_EmailInvalid(string email)
    {
        var result = Vendor.Create(TestBudgeting.TenantId, Details(email: email), null);

        Assert.Equal("Budgeting.Vendor.EmailInvalid", result.Error.Code);
    }

    [Theory]
    [InlineData("-FUEL")]
    [InlineData("FUEL-")]
    [InlineData("FLEET FUEL")]
    [InlineData("FLEET_FUEL")]
    public void A_default_budget_code_follows_the_code_string_format(string code)
    {
        var result = Vendor.Create(TestBudgeting.TenantId, Details(defaultBudgetCode: code), null);

        Assert.Equal("Budgeting.Vendor.DefaultBudgetCodeInvalidFormat", result.Error.Code);
    }

    [Fact]
    public void A_default_budget_code_need_not_exist_anywhere()
    {
        // No repository is consulted: the string only pre-fills a form later.
        var vendor = Create(Details(defaultBudgetCode: "NO-SUCH-CODE"));

        Assert.Equal("NO-SUCH-CODE", vendor.DefaultBudgetCode);
    }

    // --- Update ----------------------------------------------------------------------------------

    [Fact]
    public void Update_rewrites_every_field_stamps_the_actor_and_raises_updated()
    {
        var vendor = Create(Details(contactName: "Pat", phone: "807-555-0100"));
        var editor = Guid.NewGuid();

        var result = vendor.Update(Details(name: "Acme Energy", contactName: "Sam"), editor);

        Assert.True(result.IsSuccess);
        Assert.Equal("Acme Energy", vendor.Name);
        Assert.Equal("ACME ENERGY", vendor.NormalizedName);
        Assert.Equal("Sam", vendor.ContactName);
        Assert.Null(vendor.Phone); // a PUT: an omitted optional is cleared
        Assert.Equal(editor, vendor.ModifiedBy);
        Assert.Equal(TestBudgeting.ActorId, vendor.CreatedBy);

        var updated = Assert.IsType<VendorUpdatedDomainEvent>(Assert.Single(vendor.DomainEvents));
        Assert.Equal(vendor.Id, updated.VendorId);
        Assert.Equal(editor, updated.ActorId);
    }

    [Fact]
    public void An_update_that_changes_nothing_raises_no_event_and_stamps_nothing()
    {
        var vendor = Create(Details(contactName: "Pat"));
        var updatedAt = vendor.UpdatedAtUtc;

        // Different padding, same normalized values.
        var result = vendor.Update(Details(name: "  Acme Fuel Ltd. ", contactName: "Pat  ", phone: " "), Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Empty(vendor.DomainEvents);
        Assert.Null(vendor.ModifiedBy);
        Assert.Equal(updatedAt, vendor.UpdatedAtUtc);
    }

    [Fact]
    public void A_case_only_rename_is_a_change()
    {
        var vendor = Create(Details(name: "Acme fuel"));

        vendor.Update(Details(name: "Acme Fuel"), null);

        Assert.Equal("Acme Fuel", vendor.Name);
        Assert.IsType<VendorUpdatedDomainEvent>(Assert.Single(vendor.DomainEvents));
    }

    [Fact]
    public void An_invalid_update_changes_nothing()
    {
        var vendor = Create();

        var result = vendor.Update(Details(name: " "), Guid.NewGuid());

        Assert.Equal("Budgeting.Vendor.NameRequired", result.Error.Code);
        Assert.Equal("Acme Fuel Ltd.", vendor.Name);
        Assert.Empty(vendor.DomainEvents);
    }

    // --- Activate / deactivate --------------------------------------------------------------------

    [Fact]
    public void Deactivate_then_activate_each_raise_ActivationChanged()
    {
        var vendor = Create();
        var actor = Guid.NewGuid();

        vendor.SetActive(false, actor);
        Assert.False(vendor.IsActive);
        Assert.Equal(actor, vendor.ModifiedBy);
        var retired = Assert.IsType<VendorActivationChangedDomainEvent>(Assert.Single(vendor.DomainEvents));
        Assert.False(retired.IsActive);

        vendor.ClearDomainEvents();
        vendor.SetActive(true, actor);
        Assert.True(vendor.IsActive);
        var restored = Assert.IsType<VendorActivationChangedDomainEvent>(Assert.Single(vendor.DomainEvents));
        Assert.True(restored.IsActive);
    }

    [Fact]
    public void Setting_the_state_a_vendor_is_already_in_is_a_silent_no_op()
    {
        var vendor = Create();

        var result = vendor.SetActive(true, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Empty(vendor.DomainEvents);
        Assert.Null(vendor.ModifiedBy);
    }

    [Fact]
    public void NormalizeName_is_trim_and_upper_invariant()
    {
        Assert.Equal("ACME FUEL", Vendor.NormalizeName("  acme Fuel "));
        Assert.Equal(string.Empty, Vendor.NormalizeName(null));
    }
}
