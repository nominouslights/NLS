using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Domain.Bookings;

/// <summary>
/// One seat occupant on a booking — a real child row (not jsonb) so seat math can count
/// rows and a later batch can group manifests by billing customer. Named BookingPassenger,
/// never "Rider": Trips already has a <c>Rider</c> aggregate meaning something else
/// (an auto-derived name directory). <see cref="IsBillingCustomer"/> marks the passenger
/// who is the booking's customer themselves ("customer is travelling").
/// </summary>
public sealed class BookingPassenger : Entity, ITenantScoped
{
    private BookingPassenger()
    {
        // EF Core materialization only.
        Name = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid BookingId { get; private set; }
    public string Name { get; private set; }
    public string? Phone { get; private set; }
    public bool IsBillingCustomer { get; private set; }

    internal static BookingPassenger Create(Guid tenantId, string name, string? phone, bool isBillingCustomer) =>
        new()
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            IsBillingCustomer = isBillingCustomer,
        };
}

/// <summary>Raw passenger input for <see cref="Booking.Create"/>/<see cref="Booking.Update"/> — validated there.</summary>
public sealed record BookingPassengerDetails(string? Name, string? Phone, bool IsBillingCustomer);
