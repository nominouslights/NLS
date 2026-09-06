namespace NorthernLink.Booking.Application.Customers;

/// <summary>
/// Public contract for a customer. Phone is the display form as entered; matching against
/// searches uses the digits-only normalization server-side.
/// </summary>
public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
