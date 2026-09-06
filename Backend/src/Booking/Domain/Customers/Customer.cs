using NorthernLink.Shared.Kernel;
using NorthernLink.Booking.Domain.Customers.Events;

namespace NorthernLink.Booking.Domain.Customers;

/// <summary>
/// An individual person who books community shuttle seats — deliberately NOT a Clients-module
/// organization (Client is strictly an org; ClientContact requires a parent org). Phone is
/// stored as typed, but <see cref="PhoneDigits"/> keeps a digits-only normalization in a
/// dedicated comparison column so "204-555-0199", "(204) 555 0199" and "2045550199" all match
/// the same search. Search is name substring OR normalized-phone substring.
/// </summary>
public sealed class Customer : AggregateRoot, ITenantScoped
{
    private Customer()
    {
        // EF Core materialization only.
        Name = null!;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string? Phone { get; private set; }

    /// <summary>Digits-only form of <see cref="Phone"/> (null when no phone) — the search column.</summary>
    public string? PhoneDigits { get; private set; }

    public string? Email { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<Customer> Create(
        Guid tenantId,
        string name,
        string? phone,
        string? email,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Customer>(CustomerErrors.NameRequired);
        }

        var now = DateTimeOffset.UtcNow;
        var customer = new Customer
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Phone = Clean(phone),
            PhoneDigits = NormalizePhone(phone),
            Email = Clean(email),
            Notes = Clean(notes),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        customer.Raise(new CustomerCreatedDomainEvent(customer.Id, tenantId));
        return Result.Success(customer);
    }

    public Result Update(string name, string? phone, string? email, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(CustomerErrors.NameRequired);
        }

        Name = name.Trim();
        Phone = Clean(phone);
        PhoneDigits = NormalizePhone(phone);
        Email = Clean(email);
        Notes = Clean(notes);
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new CustomerUpdatedDomainEvent(Id, TenantId));
        return Result.Success();
    }

    /// <summary>
    /// Digits-only phone normalization, shared by the stored column and the search term so
    /// both sides of a comparison normalize identically. Null when the input has no digits.
    /// </summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
