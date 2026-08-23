using NorthernLink.Fleet.Domain.Maintenance.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>
/// The preventative-maintenance program for one vehicle model + service class (e.g.
/// "2016 Ford Transit T-150 / severe") — the routine <see cref="Items"/> and major-component
/// <see cref="Overhauls"/> with their km/month intervals. Assigned to units via
/// <see cref="PlanAssignment"/>; per-unit due status is never stored here — it is computed by
/// <see cref="PmSchedule"/> from <see cref="PmCompletion"/> records. Item and overhaul codes
/// are the stable natural keys, so <see cref="Update"/> replaces both lists wholesale.
/// </summary>
public sealed class MaintenancePlan : AggregateRoot, ITenantScoped
{
    private MaintenancePlan()
    {
        // EF Core materialization only.
        Name = null!;
        VehicleModel = null!;
        ServiceClass = null!;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string VehicleModel { get; private set; }
    public string ServiceClass { get; private set; }
    public string? Notes { get; private set; }
    public List<MaintenanceItem> Items { get; private set; } = [];
    public List<OverhaulSpec> Overhauls { get; private set; } = [];
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<MaintenancePlan> Create(
        Guid tenantId,
        string name,
        string vehicleModel,
        string serviceClass,
        string? notes,
        IReadOnlyList<MaintenanceItem> items,
        IReadOnlyList<OverhaulSpec> overhauls)
    {
        var error = Validate(name, vehicleModel, serviceClass, items, overhauls);
        if (error is not null)
        {
            return Result.Failure<MaintenancePlan>(error);
        }

        var now = DateTimeOffset.UtcNow;
        var plan = new MaintenancePlan
        {
            TenantId = tenantId,
            Name = name.Trim(),
            VehicleModel = vehicleModel.Trim(),
            ServiceClass = serviceClass.Trim(),
            Notes = Clean(notes),
            Items = [.. items],
            Overhauls = [.. overhauls],
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        plan.Raise(new MaintenancePlanCreatedDomainEvent(plan.Id, tenantId));
        return Result.Success(plan);
    }

    /// <summary>
    /// Replaces the plan's details, items, and overhauls wholesale — codes are the identity,
    /// so there is no per-line merge. Edits are retroactive by design: due status is computed
    /// from the current intervals, never stored.
    /// </summary>
    public Result Update(
        string name,
        string vehicleModel,
        string serviceClass,
        string? notes,
        IReadOnlyList<MaintenanceItem> items,
        IReadOnlyList<OverhaulSpec> overhauls)
    {
        var error = Validate(name, vehicleModel, serviceClass, items, overhauls);
        if (error is not null)
        {
            return Result.Failure(error);
        }

        Name = name.Trim();
        VehicleModel = vehicleModel.Trim();
        ServiceClass = serviceClass.Trim();
        Notes = Clean(notes);
        Items = [.. items];
        Overhauls = [.. overhauls];
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new MaintenancePlanUpdatedDomainEvent(Id, TenantId));
        return Result.Success();
    }

    private static Error? Validate(
        string name,
        string vehicleModel,
        string serviceClass,
        IReadOnlyList<MaintenanceItem> items,
        IReadOnlyList<OverhaulSpec> overhauls)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return MaintenanceErrors.NameRequired;
        }

        if (string.IsNullOrWhiteSpace(vehicleModel))
        {
            return MaintenanceErrors.VehicleModelRequired;
        }

        if (string.IsNullOrWhiteSpace(serviceClass))
        {
            return MaintenanceErrors.ServiceClassRequired;
        }

        var itemCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Code))
            {
                return MaintenanceErrors.ItemCodeRequired;
            }

            if (!itemCodes.Add(item.Code))
            {
                return MaintenanceErrors.DuplicateItemCode;
            }

            if (!HasValidInterval(item.IntervalKm, item.IntervalMonths))
            {
                return MaintenanceErrors.ItemIntervalRequired;
            }

            if (item.ShopMinutes <= 0)
            {
                return MaintenanceErrors.InvalidShopMinutes;
            }
        }

        var overhaulCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var overhaul in overhauls)
        {
            if (string.IsNullOrWhiteSpace(overhaul.Code))
            {
                return MaintenanceErrors.OverhaulCodeRequired;
            }

            if (!overhaulCodes.Add(overhaul.Code))
            {
                return MaintenanceErrors.DuplicateOverhaulCode;
            }

            if (!HasValidInterval(overhaul.IntervalKm, overhaul.IntervalMonths))
            {
                return MaintenanceErrors.OverhaulIntervalRequired;
            }

            if (overhaul.LabourHours <= 0)
            {
                return MaintenanceErrors.InvalidLabourHours;
            }

            if (overhaul.RelatedItemCodes.Any(code => !itemCodes.Contains(code)))
            {
                return MaintenanceErrors.UnknownRelatedItemCode;
            }
        }

        return null;
    }

    /// <summary>At least one interval axis present, and any axis given must be positive.</summary>
    private static bool HasValidInterval(int? intervalKm, int? intervalMonths)
    {
        if (intervalKm is <= 0 || intervalMonths is <= 0)
        {
            return false;
        }

        return intervalKm is not null || intervalMonths is not null;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
