using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Tests;

/// <summary>In-memory fake of the append-only PM-completion repository for handler tests.</summary>
internal sealed class InMemoryPmCompletionRepository : IPmCompletionRepository
{
    public List<PmCompletion> Completions { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public void Add(PmCompletion completion) => Completions.Add(completion);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
