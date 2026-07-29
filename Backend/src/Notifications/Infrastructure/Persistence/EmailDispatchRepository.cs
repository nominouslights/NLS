using Microsoft.EntityFrameworkCore;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="NotificationsDbContext"/> (tenant-filtered).</summary>
internal sealed class EmailDispatchRepository(NotificationsDbContext context) : IEmailDispatchRepository
{
    public Task<EmailDispatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.EmailDispatches.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public void Add(EmailDispatch dispatch) => context.EmailDispatches.Add(dispatch);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
