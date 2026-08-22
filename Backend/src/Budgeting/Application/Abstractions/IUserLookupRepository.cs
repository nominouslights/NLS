using NorthernLink.Budgeting.Application.Integration;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Persistence for the <see cref="UserLookup"/> replica. Reads are tenant-scoped (EF query filter
/// + RLS); the upsert runs from an integration handler under the event's tenant (pushed as the
/// ambient tenant) and is idempotent keyed on UserId.
/// </summary>
public interface IUserLookupRepository
{
    Task<UserLookup?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The tenant's users, ordered by email — the budget-owner picker's option list.</summary>
    Task<IReadOnlyList<UserLookup>> ListAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(UserLookup user, CancellationToken cancellationToken = default);
}
