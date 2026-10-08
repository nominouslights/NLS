using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.CostCentres.Update;

/// <summary>
/// Rewrites a cost centre's descriptive details.
/// <para>
/// <paramref name="Code"/> is optional and exists only so a client that sends the code back can
/// be told plainly that it cannot change: null or blank means "not supplied"; the entry's own
/// code (after trimming) is accepted; anything else is <see cref="CostCentreErrors.CodeImmutable"/>.
/// It is never written.
/// </para>
/// </summary>
public sealed record UpdateCostCentreCommand(
    Guid TenantId,
    Guid CostCentreId,
    string? Code,
    CostCentreDetails Details,
    Guid? ActorId) : ICommand;
