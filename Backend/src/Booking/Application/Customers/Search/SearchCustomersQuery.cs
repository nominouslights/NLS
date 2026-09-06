using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.Customers.Search;

/// <summary>Blank search returns the whole roster (alphabetical).</summary>
public sealed record SearchCustomersQuery(Guid TenantId, string? Search)
    : IQuery<IReadOnlyList<CustomerResponse>>;
