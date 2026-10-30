using NorthernLink.Budgeting.Domain.Qbo;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>Write-side repository for <see cref="QboConnection"/> (tenant-filtered).</summary>
public interface IQboConnectionRepository
{
    /// <summary>Every connection the tenant has had, Disconnected ones included (one per realm).</summary>
    Task<IReadOnlyList<QboConnection>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The tenant's live (Active or NeedsReconnect) connection, or null.</summary>
    Task<QboConnection?> GetLiveAsync(CancellationToken cancellationToken = default);

    void Add(QboConnection connection);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
