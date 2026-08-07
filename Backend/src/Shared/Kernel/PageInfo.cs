namespace NorthernLink.Shared.Kernel;

/// <summary>
/// Paging metadata for a result whose value is one page of a larger set. Carried by
/// <see cref="Result{TValue}.PageInfo"/> so a paged query keeps the same
/// <c>IQuery&lt;IReadOnlyList&lt;T&gt;&gt;</c> signature as an unpaged one.
/// <para>
/// <see cref="Page"/>/<see cref="PageSize"/> are null when the caller did not request
/// paging — the value is then the complete set and <see cref="TotalCount"/> is its length.
/// </para>
/// </summary>
public sealed record PageInfo(int? Page, int? PageSize, int TotalCount);

/// <summary>
/// Wire shape for a paged list endpoint — the serialized counterpart of <see cref="PageInfo"/>.
/// Every module that pages emits this identical envelope, so clients learn one shape rather
/// than one per module.
/// </summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int? Page, int? PageSize, int TotalCount)
{
    /// <summary>
    /// Builds the envelope from a result's value and its (possibly absent) paging metadata.
    /// With no metadata the items <em>are</em> the whole set, so the total is their count.
    /// </summary>
    public static PagedResponse<T> From(IReadOnlyList<T> items, PageInfo? pageInfo) =>
        new(items, pageInfo?.Page, pageInfo?.PageSize, pageInfo?.TotalCount ?? items.Count);
}
