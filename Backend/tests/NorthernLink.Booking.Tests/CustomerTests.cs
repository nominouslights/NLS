using NorthernLink.Booking.Domain.Customers;
using NorthernLink.Booking.Domain.Customers.Events;
using Xunit;

namespace NorthernLink.Booking.Tests;

/// <summary>Customer validation and the digits-only phone normalization that powers search.</summary>
public class CustomerTests
{
    [Fact]
    public void Name_is_required()
    {
        var result = Customer.Create(TestBookings.TenantId, "   ", "204-555-0199", null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(CustomerErrors.NameRequired, result.Error);
    }

    [Fact]
    public void Create_normalizes_phone_to_digits_and_raises_created_event()
    {
        var result = Customer.Create(
            TestBookings.TenantId, "  Doris Spence  ", " (204) 555-0199 ", "doris@example.ca", null);

        Assert.True(result.IsSuccess);
        var customer = result.Value;
        Assert.Equal("Doris Spence", customer.Name);
        Assert.Equal("(204) 555-0199", customer.Phone);      // display form kept as entered (trimmed)
        Assert.Equal("2045550199", customer.PhoneDigits);    // comparison column
        Assert.IsType<CustomerCreatedDomainEvent>(Assert.Single(customer.DomainEvents));
    }

    [Fact]
    public void Missing_phone_normalizes_to_null()
    {
        var customer = Customer.Create(TestBookings.TenantId, "Doris Spence", "   ", null, null).Value;

        Assert.Null(customer.Phone);
        Assert.Null(customer.PhoneDigits);
    }

    [Fact]
    public void Update_renormalizes_phone_and_raises_updated_event()
    {
        var customer = Customer.Create(TestBookings.TenantId, "Doris Spence", "204-555-0199", null, null).Value;
        customer.ClearDomainEvents();

        var result = customer.Update("Doris Spence", "431 555 0000", null, "moved to Leaf Rapids");

        Assert.True(result.IsSuccess);
        Assert.Equal("4315550000", customer.PhoneDigits);
        Assert.IsType<CustomerUpdatedDomainEvent>(Assert.Single(customer.DomainEvents));
    }

    [Theory]
    [InlineData("204-555-0199", "2045550199")]
    [InlineData("(204) 555.0199", "2045550199")]
    [InlineData("+1 204 555 0199", "12045550199")]
    [InlineData("555", "555")]
    public void NormalizePhone_strips_everything_but_digits(string input, string expected) =>
        Assert.Equal(expected, Customer.NormalizePhone(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no digits here")]
    public void NormalizePhone_returns_null_when_no_digits(string? input) =>
        Assert.Null(Customer.NormalizePhone(input));

    [Fact]
    public void Differently_formatted_phones_normalize_identically_so_search_matches()
    {
        // The read service normalizes the search term with the same function that produced
        // the stored column, so any formatting of the same number is substring-comparable.
        var stored = Customer.NormalizePhone("(204) 555-0199");
        var searched = Customer.NormalizePhone("204.555.0199");
        var partial = Customer.NormalizePhone("555-0199");

        Assert.Equal(stored, searched);
        Assert.Contains(partial!, stored!);
    }
}
