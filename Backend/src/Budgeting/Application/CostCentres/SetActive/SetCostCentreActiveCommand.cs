using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.CostCentres.SetActive;

/// <summary>Retires (<paramref name="IsActive"/> false) or restores a cost centre. Idempotent.</summary>
public sealed record SetCostCentreActiveCommand(
    Guid TenantId,
    Guid CostCentreId,
    bool IsActive,
    Guid? ActorId) : ICommand;
