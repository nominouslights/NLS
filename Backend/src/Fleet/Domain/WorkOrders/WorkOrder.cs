using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.WorkOrders.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.WorkOrders;

/// <summary>
/// A maintenance work order for a vehicle — the NL-WO-01 form's data. Created manually or
/// from inspection defects; advances through a status lifecycle and closes by logging
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

    /// <summary>
    /// The inspection defects this work order was raised against — fixed at creation (no
    /// add/remove afterwards), each given an outcome at completion. Empty on a manual work order
    /// and on every work order created before per-defect links existed: the "legacy" path, whose
    /// completion still matches <see cref="LineItems"/> against the generating inspection.
    /// </summary>
    public List<WorkOrderDefectLine> Defects { get; private set; } = [];

    /// <summary>True when this work order carries per-defect links (see <see cref="Defects"/>).</summary>
    public bool HasDefectLines => Defects.Count > 0;

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
        DateTimeOffset? dateRequiredOrOos,
        IReadOnlyList<WorkOrderDefectLine>? defects = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<WorkOrder>(WorkOrderErrors.TitleRequired);
        }

        if ((defects ?? []).Any(d => string.IsNullOrWhiteSpace(d.Item)))
        {
            return Result.Failure<WorkOrder>(WorkOrderErrors.DefectItemRequired);
        }

        var lines = (defects ?? [])
            .Select(d => d with
            {
                Item = d.Item.Trim(),
                Note = string.IsNullOrWhiteSpace(d.Note) ? null : d.Note.Trim(),
                // Outcomes are recorded only by Complete — never accepted at creation.
                Outcome = null,
                OutcomeNote = null,
            })
            .ToList();

        // (InspectionId, Item) is the line's key — the same address the defect has on its
        // inspection — so two lines for one defect would make its outcome ambiguous.
        var distinct = lines
            .Select(l => (l.InspectionId, Item: l.Item.ToUpperInvariant()))
            .Distinct()
            .Count();

        if (distinct != lines.Count)
        {
            return Result.Failure<WorkOrder>(WorkOrderErrors.DuplicateDefect);
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
            Defects = lines,
        };

        workOrder.Raise(new WorkOrderCreatedDomainEvent(workOrder.Id, vehicleId, tenantId));
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

        var previous = Status;
        Status = newStatus;

        Raise(new WorkOrderStatusChangedDomainEvent(Id, previous, newStatus));
        return Result.Success();
    }

    /// <summary>
    /// Closes the work order, recording the service that resolved it.
    ///
    /// A work order WITH defect lines needs exactly one outcome per line
    /// (<see cref="WorkOrderErrors.DefectOutcomeMissing"/>), none for anything that is not a line
    /// (<see cref="WorkOrderErrors.UnknownDefectOutcome"/>), a note on every Deferred one
    /// (<see cref="WorkOrderErrors.DeferredNoteRequired"/>), and never Deferred for an
    /// OutOfService line (<see cref="WorkOrderErrors.OutOfServiceCannotBeDeferred"/>). The
    /// outcomes are recorded on the lines; what they do to the inspections is the handler's job.
    ///
    /// A work order WITHOUT lines (manual, or legacy) completes exactly as before and accepts no
    /// outcomes. Completion never changes the vehicle's status.
    /// </summary>
    public Result Complete(Guid resolvingServiceId, IReadOnlyList<WorkOrderDefectOutcome>? outcomes = null)
    {
        if (IsTerminal)
        {
            return Result.Failure(WorkOrderErrors.Terminal);
        }

        var recorded = RecordOutcomes(outcomes ?? []);
        if (recorded.IsFailure)
        {
            return Result.Failure(recorded.Error);
        }

        Defects = recorded.Value;
        Status = WorkOrderStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        ResolvingServiceId = resolvingServiceId;

        Raise(new WorkOrderCompletedDomainEvent(Id, resolvingServiceId));
        return Result.Success();
    }

    /// <summary>Validates <paramref name="outcomes"/> against the lines and returns a copy of the lines with outcomes recorded. Mutates nothing.</summary>
    private Result<List<WorkOrderDefectLine>> RecordOutcomes(IReadOnlyList<WorkOrderDefectOutcome> outcomes)
    {
        var lines = new List<WorkOrderDefectLine>(Defects);
        var given = new WorkOrderDefectOutcome?[lines.Count];

        foreach (var outcome in outcomes)
        {
            // JsonStringEnumConverter still admits raw numbers, so an out-of-range integer
            // would otherwise reach the jsonb as a name that does not exist.
            if (!Enum.IsDefined(outcome.Outcome))
            {
                return Result.Failure<List<WorkOrderDefectLine>>(WorkOrderErrors.InvalidDefectOutcome);
            }

            var index = lines.FindIndex(l => l.Addresses(outcome.InspectionId, outcome.Item));
            if (index < 0)
            {
                return Result.Failure<List<WorkOrderDefectLine>>(WorkOrderErrors.UnknownDefectOutcome);
            }

            if (given[index] is not null)
            {
                return Result.Failure<List<WorkOrderDefectLine>>(WorkOrderErrors.DuplicateDefectOutcome);
            }

            given[index] = outcome;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            if (given[i] is not { } outcome)
            {
                return Result.Failure<List<WorkOrderDefectLine>>(WorkOrderErrors.DefectOutcomeMissing);
            }

            var note = string.IsNullOrWhiteSpace(outcome.Note) ? null : outcome.Note.Trim();

            if (outcome.Outcome == DefectRepairOutcome.Deferred)
            {
                if (lines[i].Severity == InspectionDefectSeverity.OutOfService)
                {
                    return Result.Failure<List<WorkOrderDefectLine>>(WorkOrderErrors.OutOfServiceCannotBeDeferred);
                }

                if (note is null)
                {
                    return Result.Failure<List<WorkOrderDefectLine>>(WorkOrderErrors.DeferredNoteRequired);
                }
            }

            lines[i] = lines[i] with { Outcome = outcome.Outcome, OutcomeNote = note };
        }

        return Result.Success(lines);
    }
}
