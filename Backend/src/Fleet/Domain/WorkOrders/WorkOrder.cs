using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.WorkOrders;

/// <summary>
/// A maintenance work order for a vehicle — the NL-WO-01 form's data. Created manually or
/// from an inspection's defects; advances through a status lifecycle and closes by logging
/// the service that resolved it (<see cref="ResolvingServiceId"/>). <see cref="Number"/>
/// (WO-…) is the business key printed on the form.
/// </summary>
public sealed class WorkOrder : AggregateRoot, ITenantScoped
{
    private WorkOrder()
    {
        // EF Core materialization only.
        Number = null!;
        Title = null!;
        Description = null!;
        CreatedBy = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid VehicleId { get; private set; }
    public string Number { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public WorkOrderStatus Status { get; private set; }
    public WorkOrderPriority Priority { get; private set; }
    public WorkOrderSource Source { get; private set; }
    public string? SourceRef { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? AssignedTo { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }
    public List<string> LineItems { get; private set; } = [];
    public DateTimeOffset? CompletedAt { get; private set; }
    public Guid? ResolvingServiceId { get; private set; }
    public Guid? ShopId { get; private set; }
    public decimal? AuthorizedLimitCad { get; private set; }
    public string? BudgetCode { get; private set; }
    public DateTimeOffset? DateRequiredOrOos { get; private set; }

    public bool IsTerminal => Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled;

    public static Result<WorkOrder> Create(
        Guid tenantId,
        Guid vehicleId,
        string number,
        string title,
        string? description,
        WorkOrderPriority priority,
        WorkOrderSource source,
        string? sourceRef,
        string createdBy,
        string? assignedTo,
        DateTimeOffset? dueDate,
        IReadOnlyList<string> lineItems,
        Guid? shopId,
        decimal? authorizedLimitCad,
        string? budgetCode,
        DateTimeOffset? dateRequiredOrOos)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<WorkOrder>(WorkOrderErrors.TitleRequired);
        }

        var workOrder = new WorkOrder
        {
            TenantId = tenantId,
            VehicleId = vehicleId,
            Number = number,
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Status = WorkOrderStatus.Open,
            Priority = priority,
            Source = source,
            SourceRef = string.IsNullOrWhiteSpace(sourceRef) ? null : sourceRef.Trim(),
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "Dispatch" : createdBy.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            AssignedTo = string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo.Trim(),
            DueDate = dueDate,
            LineItems = lineItems.Select(i => i.Trim()).Where(i => i.Length > 0).ToList(),
            ShopId = shopId,
            AuthorizedLimitCad = authorizedLimitCad,
            BudgetCode = string.IsNullOrWhiteSpace(budgetCode) ? null : budgetCode.Trim(),
            DateRequiredOrOos = dateRequiredOrOos,
        };

        return Result.Success(workOrder);
    }

    /// <summary>Advances the status. Completion goes through <see cref="Complete"/> instead.</summary>
    public Result ChangeStatus(WorkOrderStatus newStatus)
    {
        if (IsTerminal)
        {
            return Result.Failure(WorkOrderErrors.Terminal);
        }

        if (newStatus == WorkOrderStatus.Completed)
        {
            return Result.Failure(WorkOrderErrors.UseCompleteEndpoint);
        }

        Status = newStatus;
        return Result.Success();
    }

    /// <summary>Closes the work order, recording the service that resolved it.</summary>
    public Result Complete(Guid resolvingServiceId)
    {
        if (IsTerminal)
        {
            return Result.Failure(WorkOrderErrors.Terminal);
        }

        Status = WorkOrderStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        ResolvingServiceId = resolvingServiceId;
        return Result.Success();
    }
}
