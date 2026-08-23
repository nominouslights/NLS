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
/// check made by the handler, not here.
/// </summary>
public sealed class PmCompletion : AggregateRoot, ITenantScoped
{
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
        if (string.IsNullOrWhiteSpace(itemCode))
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.CompletionCodeRequired);
        }

        if (string.IsNullOrWhiteSpace(performedBy))
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.PerformedByRequired);
        }

        if (odometerKm < 0)
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.InvalidOdometer);
        }

        if (performedAt == default)
        {
            return Result.Failure<PmCompletion>(MaintenanceErrors.PerformedAtRequired);
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
