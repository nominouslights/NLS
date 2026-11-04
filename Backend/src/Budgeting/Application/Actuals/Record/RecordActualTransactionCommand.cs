using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Actuals.Record;

/// <summary>
/// Records one thing that actually happened with money, against a budget code, on a date.
/// Returns the new transaction's id.
/// <para>
/// There is no period here, by design: the period is derived from
/// <paramref name="OccurredOn"/> and never stored (see <c>ActualTransaction</c>). The handler
/// still checks that <em>some</em> period covers the date, so nothing is stored that the
/// period-driven console could never show.
/// </para>
/// <para>
/// <paramref name="AmountCad"/>, <paramref name="OccurredOn"/> and
/// <paramref name="Description"/> are nullable so a missing field fails as a readable domain
/// validation error rather than a model-binding 400 with no code in it. There is no Source or
/// ExternalReference: everything recorded through this command is
/// <c>ActualTransactionSource.Manual</c>, and a caller that could name its own source could
/// claim a QuickBooks id and break the re-import guard. <paramref name="ActorId"/> comes from
/// the endpoint's <c>ICurrentActor</c> — the signed token's <c>sub</c> claim — never from the
/// request body.
/// </para>
/// </summary>
public sealed record RecordActualTransactionCommand(
    Guid TenantId,
    Guid BudgetCodeId,
    decimal? AmountCad,
    DateOnly? OccurredOn,
    string? Description,
    Guid? ActorId) : ICommand<Guid>;
