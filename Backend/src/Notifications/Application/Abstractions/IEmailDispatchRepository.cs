using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Application.Abstractions;

/// <summary>Write-side persistence for the EmailDispatch aggregate (tenant-scoped).</summary>
public interface IEmailDispatchRepository
{
    /// <summary>By dispatch id — the send endpoint's idempotency replay check.</summary>
    Task<EmailDispatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(EmailDispatch dispatch);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
