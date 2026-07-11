namespace NorthernLink.SharedKernel;

/// <summary>
/// Marker for every entity that belongs to a tenant. Non-negotiable platform rule:
/// tenant-scoped data is enforced twice — an API-level authorization check AND a
/// Postgres Row-Level Security policy keyed on <see cref="TenantId"/>. Implementing
/// this interface is what opts an entity into both mechanisms.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; }
}
