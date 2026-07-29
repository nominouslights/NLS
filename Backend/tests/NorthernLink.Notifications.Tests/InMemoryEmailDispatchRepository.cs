using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Tests;

/// <summary>In-memory fake of the write-side dispatch repository for handler tests.</summary>
internal sealed class InMemoryEmailDispatchRepository : IEmailDispatchRepository
{
    public List<EmailDispatch> Dispatches { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<EmailDispatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Dispatches.FirstOrDefault(d => d.Id == id));

    public void Add(EmailDispatch dispatch) => Dispatches.Add(dispatch);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
