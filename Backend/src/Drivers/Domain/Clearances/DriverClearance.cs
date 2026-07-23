using NorthernLink.Drivers.Domain.Clearances.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Clearances;

/// <summary>
/// A client-site clearance granted to a driver (e.g. an Alamos site induction).
/// <see cref="ClientName"/> is free text — clearances routinely predate the client's
/// CRM record, and cross-module ids would couple Drivers to Clients. Revoking is a hard
/// delete of the aggregate. Clearance checks at trip-assignment time are a UI-side
/// warning in v1, so nothing here blocks assignment.
/// </summary>
public sealed class DriverClearance : AggregateRoot, ITenantScoped
{
    private DriverClearance()
    {
        // EF Core materialization only.
        Title = null!;
        ClientName = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid DriverId { get; private set; }
    public string Title { get; private set; }
    public string ClientName { get; private set; }
    public DateOnly? Expiry { get; private set; }
    public DateTimeOffset GrantedAtUtc { get; private set; }

    public static Result<DriverClearance> Grant(
        Guid tenantId,
        Guid driverId,
        string title,
        string clientName,
        DateOnly? expiry)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<DriverClearance>(ClearanceErrors.TitleRequired);
        }

        if (string.IsNullOrWhiteSpace(clientName))
        {
            return Result.Failure<DriverClearance>(ClearanceErrors.ClientNameRequired);
        }

        var clearance = new DriverClearance
        {
            TenantId = tenantId,
            DriverId = driverId,
            Title = title.Trim(),
            ClientName = clientName.Trim(),
            Expiry = expiry,
            GrantedAtUtc = DateTimeOffset.UtcNow,
        };

        clearance.Raise(new DriverClearanceGrantedDomainEvent(clearance.Id, driverId, tenantId));

        return Result.Success(clearance);
    }
}
