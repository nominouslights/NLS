using NorthernLink.Fleet.Domain.Services.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Services;

/// <summary>
/// A maintenance service record for a vehicle (NSC Standard 13) — WHO did the job
/// (<see cref="PerformedBy"/>), WHAT was changed (<see cref="ItemsChanged"/>), and WHY
/// (<see cref="Reason"/>). May close a work order (<see cref="WorkOrderId"/>).
/// <see cref="Number"/> (SVC-…) is the business key.
/// </summary>
public sealed class ServiceRecord : AggregateRoot, ITenantScoped
{
    private ServiceRecord()
    {
        // EF Core materialization only.
        Number = null!;
        PerformedBy = null!;
        Reason = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid VehicleId { get; private set; }
    public string Number { get; private set; }
    public DateTimeOffset Date { get; private set; }
    public string PerformedBy { get; private set; }
    public ServiceCategory Category { get; private set; }
    public int OdometerKm { get; private set; }
    public List<string> ItemsChanged { get; private set; } = [];
    public string Reason { get; private set; }
    public List<ServicePart> PartsUsed { get; private set; } = [];
    public decimal? LaborHours { get; private set; }
    public decimal? CostCad { get; private set; }
    public Guid? WorkOrderId { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<ServiceRecord> Log(
        Guid tenantId,
        Guid vehicleId,
        string number,
        DateTimeOffset date,
        string performedBy,
        ServiceCategory category,
        int odometerKm,
        IReadOnlyList<string> itemsChanged,
        string reason,
        IReadOnlyList<ServicePart> partsUsed,
        decimal? laborHours,
        decimal? costCad,
        Guid? workOrderId,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(performedBy))
        {
            return Result.Failure<ServiceRecord>(ServiceErrors.PerformedByRequired);
        }

        var items = itemsChanged.Select(i => i.Trim()).Where(i => i.Length > 0).ToList();
        if (items.Count == 0)
        {
            return Result.Failure<ServiceRecord>(ServiceErrors.ItemsRequired);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<ServiceRecord>(ServiceErrors.ReasonRequired);
        }

        if (odometerKm < 0)
        {
            return Result.Failure<ServiceRecord>(ServiceErrors.InvalidOdometer);
        }

        var record = new ServiceRecord
        {
            TenantId = tenantId,
            VehicleId = vehicleId,
            Number = number,
            Date = date,
            PerformedBy = performedBy.Trim(),
            Category = category,
            OdometerKm = odometerKm,
            ItemsChanged = items,
            Reason = reason.Trim(),
            PartsUsed = [.. partsUsed],
            LaborHours = laborHours,
            CostCad = costCad,
            WorkOrderId = workOrderId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        record.Raise(new ServiceRecordLoggedDomainEvent(record.Id, vehicleId, tenantId));
        return Result.Success(record);
    }
}
