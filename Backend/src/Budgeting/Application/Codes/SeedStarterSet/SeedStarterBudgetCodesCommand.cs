using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Codes.SeedStarterSet;

/// <summary>
/// Creates any of <see cref="StarterBudgetCodes"/> that <paramref name="PeriodId"/>'s chart does
/// not already have. Returns how many were created, so the caller can say "12 codes added" rather
/// than "done".
/// <para>
/// Idempotent per period, by code string: re-running on the same period creates nothing and
/// returns 0, which is what makes it safe to expose as a button rather than a one-shot. Another
/// period's chart has no bearing — each period seeds its own.
/// </para>
/// </summary>
public sealed record SeedStarterBudgetCodesCommand(Guid TenantId, Guid PeriodId, Guid? ActorId) : ICommand<int>;
