namespace NorthernLink.Budgeting.Application.Periods;

/// <summary>
/// The Budgeting module's public representation of a budget period — the shape the
/// Budgeting console's periods screen consumes. <paramref name="Granularity"/> and
/// <paramref name="State"/> are the enum names as strings (Month/Quarter, Draft/Open/Locked).
/// Dates are inclusive. No planned/allocated figures on the wire — those belong to the
/// allocations story.
/// </summary>
public sealed record BudgetPeriodResponse(
    Guid Id,
    string Label,
    string Granularity,
    int Year,
    int Ordinal,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
