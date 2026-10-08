namespace NorthernLink.Budgeting.Domain.Vendors;

/// <summary>
/// Everything a person types about a vendor, as one parameter object shared by
/// <see cref="Vendor.Create"/> and <see cref="Vendor.Update"/> — the <c>BudgetCodeDetails</c>
/// shape, so create and edit cannot drift apart. Raw input: strings may be padded or blank, and
/// the aggregate trims them and stores blank as null.
/// </summary>
public sealed record VendorDetails
{
    /// <summary>The vendor's name. Required, ≤ <see cref="Vendor.NameMaxLength"/> after trim, unique per tenant ignoring case.</summary>
    public string? Name { get; init; }

    public string? ContactName { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? Address { get; init; }

    public string? Notes { get; init; }

    /// <summary>Reference data only — see <see cref="Vendor.GstRegistrationNumber"/>.</summary>
    public string? GstRegistrationNumber { get; init; }

    /// <summary>The vendor's name as it appears in QuickBooks — see <see cref="Vendor.QboDisplayName"/>.</summary>
    public string? QboDisplayName { get; init; }

    /// <summary>A budget code string — see <see cref="Vendor.DefaultBudgetCode"/>.</summary>
    public string? DefaultBudgetCode { get; init; }
}
