using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Tests;

/// <summary>In-memory fake of the write-side template repository for handler tests.</summary>
internal sealed class InMemoryEmailTemplateRepository : IEmailTemplateRepository
{
    public List<EmailTemplate> Templates { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<EmailTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Templates.FirstOrDefault(t => t.Id == id));

    public void Add(EmailTemplate template) => Templates.Add(template);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
