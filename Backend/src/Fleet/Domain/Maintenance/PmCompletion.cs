using NorthernLink.Fleet.Domain.Maintenance.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>
/// One completed preventative-maintenance service on one vehicle — the append-only record
/// that a plan item or overhaul (<see cref="ItemCode"/>, <see cref="Kind"/>) was certified at
/// <see cref="OdometerKm"/> on <see cref="PerformedAt"/>. The latest completion per code is
/// the basis <see cref="PmSchedule"/> computes due status from. May carry the shop's
/// <see cref="Measurement"/> (pad mm, compression psi, …) and an optional link to the work
/// order that covered it. Whether the code exists in the assigned plan is a cross-aggregate
/// check made by the handler, not here. Deliberately separate from the NSC ServiceRecord
/// shop log: this is the plan-item-keyed PM ledger due math runs on, ServiceRecord is the
/// free-text shop history — unify later only if double entry proves painful.
/// </summary>
public sealed class PmCompletion : AggregateRoot, ITenantScoped
{
    /// <summary>DB column cap on <see cref="PerformedBy"/> (pm_completions.performed_by).</summary>
    public const int PerformedByMaxLength = 200;

    /// <summary>DB column cap on <see cref="Measurement"/> (pm_completions.measurement).</summary>
    public const int MeasurementMaxLength = 500;

    /// <summary>DB column cap on <see cref="Notes"/> (pm_completions.notes).</summary>
    public const int NotesMaxLength = 2000;

    private PmCompletion()
    {
        // EF Core materialization only.
        ItemCode = null!;
        PerformedBy = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid PlanId { get; private set; }
    public string ItemCode { get; private set; }
    public PmEntryKind Kind { get; private set; }
    public DateOnly PerformedAt { get; private set; }
    public int OdometerKm { get; private set; }
    public string PerformedBy { get; private set; }
    public Guid? WorkOrderId { get; private set; }
    public string? Measurement { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<PmCompletion> Log(
        Guid tenantId,
        Guid vehicleId,
        Guid planId,
        string itemCode,
        PmEntryKind kind,
        DateOnly performedAt,
        int odometerKm,
        string performedBy,
        Guid? workOrderId,
        string? measurement,
        string? notes)
    {
        if (vehicleId == Guid.Empty)
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.VehicleRequired);
        }

        if (planId == Guid.Empty)
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.PlanRequired);
        }

        if (string.IsNullOrWhiteSpace(itemCode))
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.CompletionCodeRequired);
        }

        if (itemCode.Trim().Length > MaintenancePlan.CodeMaxLength)
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.CompletionCodeTooLong);
        }

        if (string.IsNullOrWhiteSpace(performedBy))
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.PerformedByRequired);
        }

        if (performedBy.Trim().Length > PerformedByMaxLength)
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.PerformedByTooLong);
        }

        if (measurement?.Trim().Length > MeasurementMaxLength)
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.MeasurementTooLong);
        }

        if (notes?.Trim().Length > NotesMaxLength)
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.CompletionNotesTooLong);
        }

        if (odometerKm < 0)
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.InvalidOdometer);
        }

        if (performedAt == default)
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.PerformedAtRequired);
        }

        // One day of grace absorbs the shop being a timezone ahead of UTC; anything further
        // out is a typo, not a completion.
        var todayUtc = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        if (performedAt > todayUtc.AddDays(1))
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.PerformedAtInFuture);
        }

        var completion = new PmCompletion
        {
            TenantId = tenantId,
            VehicleId = vehicleId,
            PlanId = planId,
            ItemCode = itemCode.Trim(),
            Kind = kind,
            PerformedAt = performedAt,
            OdometerKm = odometerKm,
            PerformedBy = performedBy.Trim(),
            WorkOrderId = workOrderId,
            Measurement = Clean(measurement),
            Notes = Clean(notes),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        completion.Raise(new PmCompletionLoggedDomainEvent(completion.Id, vehicleId, tenantId));
        return Result.Success(completion);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
