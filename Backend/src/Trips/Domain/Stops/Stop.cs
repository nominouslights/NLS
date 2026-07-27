using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Stops.Events;

namespace NorthernLink.Trips.Domain.Stops;

/// <summary>
/// A reusable catalog stop with a structured address and real lat/lng coordinates
/// (sourced from the dispatcher's Google Places selection). Routes reference stops by id
/// and snapshot their name + coordinates onto the ordered <c>RouteStop</c> list, so the
/// map can be populated later. Deactivating hides a stop from new route building without
/// touching routes that already snapshotted it. Mirrors the Route aggregate shape.
/// </summary>
public sealed class Stop : AggregateRoot, ITenantScoped
{
    private Stop()
    {
        // EF Core materialization only.
        Name = null!;
        Address = null!;
        Coordinate = null!;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public StopType? Type { get; private set; }
    public Address Address { get; private set; }
    public Coordinate Coordinate { get; private set; }
    public string? Notes { get; private set; }
    public bool Active { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<Stop> Create(
        Guid tenantId,
        string name,
        StopType? type,
        Address address,
        Coordinate coordinate,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Stop>(StopErrors.NameRequired);
        }

        var now = DateTimeOffset.UtcNow;
        var stop = new Stop
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Type = type,
            Address = address,
            Coordinate = coordinate,
            Notes = Clean(notes),
            Active = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        stop.Raise(new StopCreatedDomainEvent(stop.Id));
        return Result.Success(stop);
    }

    public Result Update(
        string name,
        StopType? type,
        Address address,
        Coordinate coordinate,
        string? notes,
        bool active)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(StopErrors.NameRequired);
        }

        Name = name.Trim();
        Type = type;
        Address = address;
        Coordinate = coordinate;
        Notes = Clean(notes);
        Active = active;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new StopUpdatedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>Activates or deactivates the stop (mirrors ScheduleTemplate.Activate/Deactivate).</summary>
    public void SetActive(bool active)
    {
        if (Active == active)
        {
            return;
        }

        Active = active;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new StopUpdatedDomainEvent(Id));
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
