using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Schedules;

/// <summary>
/// One dated departure from a template's recurrence — a skipped occurrence, an extra run,
/// or a time change (see <see cref="ScheduleExceptionKind"/>). A child entity of the
/// <see cref="ScheduleTemplate"/> aggregate, on the <c>ShipmentLeg</c> model: a real table
/// (the generation worker and the special-dates UI both query by date), reached and
/// mutated only through the template, which owns every invariant — one exception per date,
/// and the per-kind time rules.
/// <para>
/// Exceptions are <b>generation-time only</b>: they change what the worker materializes
/// from here on, and never touch trips that already exist. Skipping a date whose trip was
/// already generated is a no-op — cancelling that trip is a dispatcher action on the trip
/// itself. Past dates are deliberately not rejected (a backdated skip documents why a run
/// never happened), they simply fall outside every future generation window.
/// </para>
/// </summary>
public sealed class ScheduleException : Entity, ITenantScoped
{
    private ScheduleException()
    {
        // EF Core materialization only.
    }

    /// <summary>
    /// Copied from the owning template. Redundant in the object model, non-negotiable in the
    /// database: every tenant-scoped table carries its own <c>tenant_id</c> with its own RLS
    /// policy — isolation is never inherited through a foreign key.
    /// </summary>
    public Guid TenantId { get; private set; }

    public Guid ScheduleTemplateId { get; private set; }

    /// <summary>The service date the exception applies to (for a paired template, the outbound's date).</summary>
    public DateOnly Date { get; private set; }

    public ScheduleExceptionKind Kind { get; private set; }

    /// <summary>ExtraRun: the run's departure (required). TimeOverride: replaces the template's departure when set. Always null for Skip.</summary>
    public TimeOnly? DepartureTime { get; private set; }

    /// <summary>ExtraRun: same-day return leg iff set. TimeOverride: replaces the template's return departure when set. Always null for Skip.</summary>
    public TimeOnly? ReturnDepartureTime { get; private set; }

    /// <summary>Free-form reason ("Treaty Days", "road closure"); trimmed, blank stored as null.</summary>
    public string? Note { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    internal static ScheduleException Create(
        Guid tenantId,
        Guid scheduleTemplateId,
        DateOnly date,
        ScheduleExceptionKind kind,
        TimeOnly? departureTime,
        TimeOnly? returnDepartureTime,
        string? note)
    {
        var now = DateTimeOffset.UtcNow;
        return new ScheduleException
        {
            TenantId = tenantId,
            ScheduleTemplateId = scheduleTemplateId,
            Date = date,
            Kind = kind,
            DepartureTime = departureTime,
            ReturnDepartureTime = returnDepartureTime,
            Note = Normalize(note),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    internal void Update(
        DateOnly date,
        ScheduleExceptionKind kind,
        TimeOnly? departureTime,
        TimeOnly? returnDepartureTime,
        string? note)
    {
        Date = date;
        Kind = kind;
        DepartureTime = departureTime;
        ReturnDepartureTime = returnDepartureTime;
        Note = Normalize(note);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
