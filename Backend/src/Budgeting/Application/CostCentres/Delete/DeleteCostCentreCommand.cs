using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.CostCentres.Delete;

/// <summary>
/// Permanently removes a cost centre created in error that nothing carries. No ActorId — the
/// <c>DeleteBudgetCodeCommand</c> reasoning: a deleted row has nowhere to record who deleted it,
/// and the audit pipeline keeps its final snapshot.
/// </summary>
public sealed record DeleteCostCentreCommand(Guid TenantId, Guid CostCentreId) : ICommand;
