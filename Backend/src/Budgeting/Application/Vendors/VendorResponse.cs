namespace NorthernLink.Budgeting.Application.Vendors;

/// <summary>
/// The Budgeting module's public representation of a vendor — every stored field except the
/// internal uniqueness key. Optional text is null when blank, never an empty string.
/// <see cref="GstRegistrationNumber"/> is reference data; nothing derives tax from it.
/// </summary>
public sealed record VendorResponse(
    Guid Id,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    string? Notes,
    string? GstRegistrationNumber,
    string? QboDisplayName,
    string? DefaultBudgetCode,
    bool IsActive,
    Guid? CreatedBy,
    Guid? ModifiedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
