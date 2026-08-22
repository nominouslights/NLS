using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Codes.SeedStarterSet;

/// <summary>
/// Creates any of <see cref="StarterBudgetCodes"/> the tenant does not already have. Returns how
/// many were created, so the caller can say "12 codes added" rather than "done".
/// <para>
/// Idempotent by code string: re-running creates nothing and returns 0, which is what makes it
/// safe to expose as a button rather than a one-shot.
/// </para>
/// </summary>
public sealed record SeedStarterBudgetCodesCommand(Guid TenantId, Guid? ActorId) : ICommand<int>;
