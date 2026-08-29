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
    /// <summary>DB column cap on <see cref="Name"/> (maintenance_plans.name).</summary>
    public const int NameMaxLength = 200;

    /// <summary>DB column cap on <see cref="VehicleModel"/> (maintenance_plans.vehicle_model).</summary>
    public const int VehicleModelMaxLength = 200;

    /// <summary>DB column cap on <see cref="ServiceClass"/> (maintenance_plans.service_class).</summary>
    public const int ServiceClassMaxLength = 64;

    /// <summary>DB column cap on <see cref="Notes"/> (maintenance_plans.notes).</summary>
    public const int NotesMaxLength = 2000;

    /// <summary>
    /// Cap on item and overhaul codes — the natural key completions reference, so it matches
    /// the pm_completions.item_code column (varchar(32)).
    /// </summary>
    public const int CodeMaxLength = 32;

    /// <summary>
    /// Sanity cap on km intervals — the seed data tops out at 320,000 km, so anything past a
    /// million km is a typo, and the cap keeps <see cref="PmSchedule"/>'s next-due km math
    /// far from <see cref="int.MaxValue"/>.
    /// </summary>
    public const int MaxIntervalKm = 1_000_000;

    /// <summary>Sanity cap on month intervals (50 years; the seed data tops out at 180 months).</summary>
    public const int MaxIntervalMonths = 600;

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
        // Normalize before validating, so duplicate detection, the related-code cross-check,
        // and the stored natural keys all operate on the trimmed values — aligned with
        // PmCompletion.ItemCode, which is trimmed the same way.
        var normalizedItems = items.Select(Normalize).ToList();
        var normalizedOverhauls = overhauls.Select(Normalize).ToList();

        var error = Validate(name, vehicleModel, serviceClass, notes, normalizedItems, normalizedOverhauls);
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
            Items = normalizedItems,
            Overhauls = normalizedOverhauls,
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
        var normalizedItems = items.Select(Normalize).ToList();
        var normalizedOverhauls = overhauls.Select(Normalize).ToList();

        var error = Validate(name, vehicleModel, serviceClass, notes, normalizedItems, normalizedOverhauls);
        if (error is not null)
        {
            return Result.Failure(error);
        }

        Name = name.Trim();
        VehicleModel = vehicleModel.Trim();
        ServiceClass = serviceClass.Trim();
        Notes = Clean(notes);
        Items = normalizedItems;
        Overhauls = normalizedOverhauls;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new MaintenancePlanUpdatedDomainEvent(Id, TenantId));
        return Result.Success();
    }

    private static Error? Validate(
        string name,
        string vehicleModel,
        string serviceClass,
        string? notes,
        IReadOnlyList<MaintenanceItem> items,
        IReadOnlyList<OverhaulSpec> overhauls)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return MaintenanceErrors.NameRequired;
        }

        if (name.Trim().Length > NameMaxLength)
        {
            return MaintenanceErrors.NameTooLong;
        }

        if (string.IsNullOrWhiteSpace(vehicleModel))
        {
            return MaintenanceErrors.VehicleModelRequired;
        }

        if (vehicleModel.Trim().Length > VehicleModelMaxLength)
        {
            return MaintenanceErrors.VehicleModelTooLong;
        }

        if (string.IsNullOrWhiteSpace(serviceClass))
        {
            return MaintenanceErrors.ServiceClassRequired;
        }

        if (serviceClass.Trim().Length > ServiceClassMaxLength)
        {
            return MaintenanceErrors.ServiceClassTooLong;
        }

        if (notes?.Trim().Length > NotesMaxLength)
        {
            return MaintenanceErrors.NotesTooLong;
        }

        var itemCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            // JsonStringEnumConverter still admits raw numbers, so an out-of-range integer
            // binds silently — reject anything not a declared member (Budgeting precedent).
            if (!Enum.IsDefined(item.Tier))
            {
                return MaintenanceErrors.InvalidComponentTier;
            }

            if (!Enum.IsDefined(item.Task))
            {
                return MaintenanceErrors.InvalidMaintenanceTask;
            }

            var error = ValidateEntry(
                item.Code,
                item.IntervalKm,
                item.IntervalMonths,
                effortIsPositive: item.ShopMinutes > 0,
                item.LeadKm,
                item.LeadDays,
                itemCodes,
                ItemEntryErrors);
            if (error is not null)
            {
                return error;
            }
        }

        var overhaulCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var overhaul in overhauls)
        {
            var error = ValidateEntry(
                overhaul.Code,
                overhaul.IntervalKm,
                overhaul.IntervalMonths,
                effortIsPositive: overhaul.LabourHours > 0,
                overhaul.LeadKm,
                overhaul.LeadDays,
                overhaulCodes,
                OverhaulEntryErrors);
            if (error is not null)
            {
                return error;
            }

            if (overhaul.PartsCad < 0)
            {
                return MaintenanceErrors.InvalidPartsCad;
            }

            if (overhaul.RelatedItemCodes.Any(code => !itemCodes.Contains(code)))
            {
                return MaintenanceErrors.UnknownRelatedItemCode;
            }
        }

        return null;
    }

    /// <summary>The per-line checks items and overhauls share; the caller adds any kind-specific extras.</summary>
    private static Error? ValidateEntry(
        string code,
        int? intervalKm,
        int? intervalMonths,
        bool effortIsPositive,
        int? leadKm,
        int? leadDays,
        HashSet<string> seenCodes,
        EntryErrors errors)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return errors.CodeRequired;
        }

        if (code.Length > CodeMaxLength)
        {
            return errors.CodeTooLong;
        }

        if (!seenCodes.Add(code))
        {
            return errors.DuplicateCode;
        }

        if (!HasValidInterval(intervalKm, intervalMonths))
        {
            return errors.IntervalRequired;
        }

        if (intervalKm > MaxIntervalKm)
        {
            return MaintenanceErrors.IntervalKmTooLarge;
        }

        if (intervalMonths > MaxIntervalMonths)
        {
            return MaintenanceErrors.IntervalMonthsTooLarge;
        }

        if (!effortIsPositive)
        {
            return errors.InvalidEffort;
        }

        if (leadKm is <= 0)
        {
            return MaintenanceErrors.InvalidLeadKm;
        }

        if (leadDays is <= 0)
        {
            return MaintenanceErrors.InvalidLeadDays;
        }

        // A lead at or past its own interval arm would pin the line to DueSoon forever —
        // the moment a completion lands, the next one is already "due soon".
        if (leadKm >= intervalKm)
        {
            return MaintenanceErrors.LeadKmNotBelowInterval;
        }

        // 28 days is the conservative month floor: a lead shorter than intervalMonths×28
        // can never cover the whole calendar interval, whatever the actual month lengths.
        if (leadDays >= intervalMonths * 28)
        {
            return MaintenanceErrors.LeadDaysNotBelowInterval;
        }

        return null;
    }

    private sealed record EntryErrors(
        Error CodeRequired,
        Error CodeTooLong,
        Error DuplicateCode,
        Error IntervalRequired,
        Error InvalidEffort);

    private static readonly EntryErrors ItemEntryErrors = new(
        MaintenanceErrors.ItemCodeRequired,
        MaintenanceErrors.ItemCodeTooLong,
        MaintenanceErrors.DuplicateItemCode,
        MaintenanceErrors.ItemIntervalRequired,
        MaintenanceErrors.InvalidShopMinutes);

    private static readonly EntryErrors OverhaulEntryErrors = new(
        MaintenanceErrors.OverhaulCodeRequired,
        MaintenanceErrors.OverhaulCodeTooLong,
        MaintenanceErrors.DuplicateOverhaulCode,
        MaintenanceErrors.OverhaulIntervalRequired,
        MaintenanceErrors.InvalidLabourHours);

    /// <summary>At least one interval axis present, and any axis given must be positive.</summary>
    private static bool HasValidInterval(int? intervalKm, int? intervalMonths) =>
        intervalKm is null or > 0
        && intervalMonths is null or > 0
        && (intervalKm is not null || intervalMonths is not null);

    private static MaintenanceItem Normalize(MaintenanceItem item) => item with
    {
        Code = item.Code?.Trim()!,
        System = item.System?.Trim()!,
        Component = item.Component?.Trim()!,
        Notes = Clean(item.Notes),
    };

    private static OverhaulSpec Normalize(OverhaulSpec overhaul) => overhaul with
    {
        Code = overhaul.Code?.Trim()!,
        Component = overhaul.Component?.Trim()!,
        Scope = overhaul.Scope?.Trim()!,
        ConditionTriggers = NormalizeList(overhaul.ConditionTriggers),
        RelatedItemCodes = NormalizeList(overhaul.RelatedItemCodes),
    };

    /// <summary>Trims every entry and drops the blank ones.</summary>
    private static List<string> NormalizeList(IEnumerable<string> values) =>
        [.. values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim())];

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
