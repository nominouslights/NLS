using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Schedules.Events;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Domain.Schedules;

/// <summary>
/// A weekly recurring service pattern over a route ("Alamos crew shuttle, Mon–Fri
/// 06:30 out / 17:30 back"). The generation worker expands active templates
/// <see cref="GenerationHorizonDays"/> days ahead into concrete trips: one Outbound leg
/// per matching day, plus — when <see cref="ReturnDepartureTime"/> is set — an Inbound
/// return leg, the pair sharing a RoundTripKey (<c>{templateId:N}:{yyyyMMdd}</c>) that
/// Billing later prices as one round trip. Client identity is a snapshot (id + name from
/// client_lookup), not a reference — editing a template only affects trips not yet
/// generated. Deactivate stops future generation; existing trips are untouched.
/// </summary>
public sealed class ScheduleTemplate : AggregateRoot, ITenantScoped
{
    public const int MaxHorizonDays = 60;
    public const int DefaultHorizonDays = 7;

    private ScheduleTemplate()
    {
        // EF Core materialization only.
        Name = null!;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public Guid RouteId { get; private set; }
    public TripServiceType ServiceType { get; private set; }
    public Guid? ClientId { get; private set; }
    public string? ClientName { get; private set; }

    /// <summary>Which of the three recurrence models this template uses; only the matching fields below are populated.</summary>
    public ScheduleRecurrenceKind RecurrenceKind { get; private set; }

    /// <summary>DaysOfWeek recurrence: emit on each listed weekday. Empty for other kinds.</summary>
    public List<DayOfWeek> DaysOfWeek { get; private set; } = [];

    /// <summary>EveryNDays recurrence: the interval in days (1..365). Null for other kinds.</summary>
    public int? IntervalDays { get; private set; }

    /// <summary>EveryNDays recurrence: the date the interval is counted from. Null for other kinds.</summary>
    public DateOnly? AnchorDate { get; private set; }

    /// <summary>MonthlyDays recurrence: calendar days (1..31), clamped to month-end. Empty for other kinds.</summary>
    public List<int> DaysOfMonth { get; private set; } = [];

    public TimeOnly DepartureTime { get; private set; }

    /// <summary>Non-null ⇒ each occurrence generates an outbound + return leg pair sharing a RoundTripKey.</summary>
    public TimeOnly? ReturnDepartureTime { get; private set; }

    public int SeatsCapacity { get; private set; }

    /// <summary>Community-run viability threshold; surfacing it is a frontend concern.</summary>
    public int? SeatsMinimum { get; private set; }

    public string? DefaultVehicleUnit { get; private set; }
    public Guid? DefaultDriverId { get; private set; }
    public int GenerationHorizonDays { get; private set; }
    public string? CutoffNote { get; private set; }
    public bool Active { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<ScheduleTemplate> Create(
        Guid tenantId,
        string name,
        Guid routeId,
        TripServiceType serviceType,
        Guid? clientId,
        string? clientName,
        ScheduleRecurrenceKind recurrenceKind,
        IReadOnlyList<DayOfWeek> daysOfWeek,
        int? intervalDays,
        DateOnly? anchorDate,
        IReadOnlyList<int> daysOfMonth,
        TimeOnly departureTime,
        TimeOnly? returnDepartureTime,
        int seatsCapacity,
        int? seatsMinimum,
        string? defaultVehicleUnit,
        Guid? defaultDriverId,
        int generationHorizonDays = DefaultHorizonDays,
        string? cutoffNote = null)
    {
        var validation = Validate(
            name, routeId, recurrenceKind, daysOfWeek, intervalDays, anchorDate, daysOfMonth,
            departureTime, returnDepartureTime, seatsCapacity, seatsMinimum, generationHorizonDays);
        if (validation.IsFailure)
        {
            return Result.Failure<ScheduleTemplate>(validation.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var template = new ScheduleTemplate
        {
            TenantId = tenantId,
            Name = name.Trim(),
            RouteId = routeId,
            ServiceType = serviceType,
            ClientId = clientId,
            ClientName = Normalize(clientName),
            DepartureTime = departureTime,
            ReturnDepartureTime = returnDepartureTime,
            SeatsCapacity = seatsCapacity,
            SeatsMinimum = seatsMinimum,
            DefaultVehicleUnit = Normalize(defaultVehicleUnit),
            DefaultDriverId = defaultDriverId,
            GenerationHorizonDays = generationHorizonDays,
            CutoffNote = Normalize(cutoffNote),
            Active = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        template.ApplyRecurrence(recurrenceKind, daysOfWeek, intervalDays, anchorDate, daysOfMonth);

        template.Raise(new ScheduleTemplateCreatedDomainEvent(template.Id));
        return Result.Success(template);
    }

    /// <summary>Edits affect only trips not yet generated — never already-materialized ones.</summary>
    public Result Update(
        string name,
        Guid routeId,
        TripServiceType serviceType,
        Guid? clientId,
        string? clientName,
        ScheduleRecurrenceKind recurrenceKind,
        IReadOnlyList<DayOfWeek> daysOfWeek,
        int? intervalDays,
        DateOnly? anchorDate,
        IReadOnlyList<int> daysOfMonth,
        TimeOnly departureTime,
        TimeOnly? returnDepartureTime,
        int seatsCapacity,
        int? seatsMinimum,
        string? defaultVehicleUnit,
        Guid? defaultDriverId,
        int generationHorizonDays,
        string? cutoffNote)
    {
        var validation = Validate(
            name, routeId, recurrenceKind, daysOfWeek, intervalDays, anchorDate, daysOfMonth,
            departureTime, returnDepartureTime, seatsCapacity, seatsMinimum, generationHorizonDays);
        if (validation.IsFailure)
        {
            return validation;
        }

        Name = name.Trim();
        RouteId = routeId;
        ServiceType = serviceType;
        ClientId = clientId;
        ClientName = Normalize(clientName);
        ApplyRecurrence(recurrenceKind, daysOfWeek, intervalDays, anchorDate, daysOfMonth);
        DepartureTime = departureTime;
        ReturnDepartureTime = returnDepartureTime;
        SeatsCapacity = seatsCapacity;
        SeatsMinimum = seatsMinimum;
        DefaultVehicleUnit = Normalize(defaultVehicleUnit);
        DefaultDriverId = defaultDriverId;
        GenerationHorizonDays = generationHorizonDays;
        CutoffNote = Normalize(cutoffNote);
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new ScheduleTemplateUpdatedDomainEvent(Id));
        return Result.Success();
    }

    public void Activate()
    {
        if (!Active)
        {
            Active = true;
            UpdatedAtUtc = DateTimeOffset.UtcNow;
            Raise(new ScheduleTemplateActivationChangedDomainEvent(Id));
        }
    }

    public void Deactivate()
    {
        if (Active)
        {
            Active = false;
            UpdatedAtUtc = DateTimeOffset.UtcNow;
            Raise(new ScheduleTemplateActivationChangedDomainEvent(Id));
        }
    }

    private static Result Validate(
        string name,
        Guid routeId,
        ScheduleRecurrenceKind recurrenceKind,
        IReadOnlyList<DayOfWeek> daysOfWeek,
        int? intervalDays,
        DateOnly? anchorDate,
        IReadOnlyList<int> daysOfMonth,
        TimeOnly departureTime,
        TimeOnly? returnDepartureTime,
        int seatsCapacity,
        int? seatsMinimum,
        int generationHorizonDays)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(ScheduleTemplateErrors.NameRequired);
        }

        if (routeId == Guid.Empty)
        {
            return Result.Failure(ScheduleTemplateErrors.RouteRequired);
        }

        var recurrenceValidation = recurrenceKind switch
        {
            ScheduleRecurrenceKind.DaysOfWeek => daysOfWeek.Count == 0
                ? Result.Failure(ScheduleTemplateErrors.AtLeastOneDay)
                : Result.Success(),
            ScheduleRecurrenceKind.EveryNDays => intervalDays is not { } interval || interval is < 1 or > 365
                ? Result.Failure(ScheduleTemplateErrors.InvalidInterval)
                : anchorDate is null
                    ? Result.Failure(ScheduleTemplateErrors.AnchorRequired)
                    : Result.Success(),
            ScheduleRecurrenceKind.MonthlyDays => daysOfMonth.Count == 0
                ? Result.Failure(ScheduleTemplateErrors.AtLeastOneDayOfMonth)
                : daysOfMonth.Any(day => day is < 1 or > 31)
                    ? Result.Failure(ScheduleTemplateErrors.InvalidDayOfMonth)
                    : Result.Success(),
            _ => Result.Success(),
        };
        if (recurrenceValidation.IsFailure)
        {
            return recurrenceValidation;
        }

        if (seatsCapacity <= 0 || seatsMinimum is { } minimum && (minimum <= 0 || minimum > seatsCapacity))
        {
            return Result.Failure(ScheduleTemplateErrors.InvalidSeats);
        }

        if (generationHorizonDays is < 1 or > MaxHorizonDays)
        {
            return Result.Failure(ScheduleTemplateErrors.InvalidHorizon);
        }

        if (returnDepartureTime is { } returnTime && returnTime <= departureTime)
        {
            return Result.Failure(ScheduleTemplateErrors.ReturnBeforeDeparture);
        }

        return Result.Success();
    }

    /// <summary>
    /// Assigns the recurrence fields for <paramref name="recurrenceKind"/>, keeping only that
    /// kind's fields populated and clearing the others — so a template never carries stale
    /// data from a previously-selected kind. Validation runs first, so inputs are known good.
    /// </summary>
    private void ApplyRecurrence(
        ScheduleRecurrenceKind recurrenceKind,
        IReadOnlyList<DayOfWeek> daysOfWeek,
        int? intervalDays,
        DateOnly? anchorDate,
        IReadOnlyList<int> daysOfMonth)
    {
        RecurrenceKind = recurrenceKind;
        switch (recurrenceKind)
        {
            case ScheduleRecurrenceKind.DaysOfWeek:
                DaysOfWeek = NormalizeDays(daysOfWeek);
                IntervalDays = null;
                AnchorDate = null;
                DaysOfMonth = [];
                break;
            case ScheduleRecurrenceKind.EveryNDays:
                DaysOfWeek = [];
                IntervalDays = intervalDays;
                AnchorDate = anchorDate;
                DaysOfMonth = [];
                break;
            case ScheduleRecurrenceKind.MonthlyDays:
                DaysOfWeek = [];
                IntervalDays = null;
                AnchorDate = null;
                DaysOfMonth = NormalizeDaysOfMonth(daysOfMonth);
                break;
        }
    }

    private static List<DayOfWeek> NormalizeDays(IReadOnlyList<DayOfWeek> daysOfWeek) =>
        [.. daysOfWeek.Distinct().OrderBy(day => day)];

    private static List<int> NormalizeDaysOfMonth(IReadOnlyList<int> daysOfMonth) =>
        [.. daysOfMonth.Distinct().OrderBy(day => day)];

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
