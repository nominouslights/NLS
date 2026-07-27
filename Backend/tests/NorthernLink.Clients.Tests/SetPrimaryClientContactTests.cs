using NorthernLink.Clients.Application.ClientContacts.SetPrimary;
using NorthernLink.Clients.Domain.ClientContacts;
using NorthernLink.Clients.Domain.ClientContacts.Events;
using Xunit;

namespace NorthernLink.Clients.Tests;

/// <summary>Set-primary handler behavior: demote/promote, not-found, and no-op.</summary>
public class SetPrimaryClientContactTests
{
    private static ClientContact Contact(Guid clientId, string name, bool isPrimary) =>
        ClientContact.Create(TestClients.TenantId, clientId, name, "Coordinator", null, null, null, isPrimary).Value;

    [Fact]
    public async Task Set_primary_demotes_the_old_primary_and_promotes_the_target()
    {
        var clientId = Guid.NewGuid();
        var oldPrimary = Contact(clientId, "Ada", isPrimary: true);
        var target = Contact(clientId, "Ben", isPrimary: false);
        var repo = new InMemoryClientContactRepository();
        repo.Add(oldPrimary);
        repo.Add(target);
        oldPrimary.ClearDomainEvents();
        target.ClearDomainEvents();
        var handler = new SetPrimaryClientContactCommandHandler(repo);

        var result = await handler.Handle(
            new SetPrimaryClientContactCommand(TestClients.TenantId, clientId, target.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(oldPrimary.IsPrimary);
        Assert.True(target.IsPrimary);

        // Both rows must raise the event so both read-model rows re-project.
        Assert.IsType<ClientContactPrimaryChangedDomainEvent>(Assert.Single(oldPrimary.DomainEvents));
        Assert.IsType<ClientContactPrimaryChangedDomainEvent>(Assert.Single(target.DomainEvents));

        // Demote flushed before promote: two separate SaveChanges against the non-deferrable index.
        Assert.Equal(2, repo.SaveChangesCallCount);
    }

    [Fact]
    public async Task Promoting_when_no_contact_is_primary_yet_sets_the_target()
    {
        var clientId = Guid.NewGuid();
        var target = Contact(clientId, "Cara", isPrimary: false);
        var repo = new InMemoryClientContactRepository();
        repo.Add(target);
        var handler = new SetPrimaryClientContactCommandHandler(repo);

        var result = await handler.Handle(
            new SetPrimaryClientContactCommand(TestClients.TenantId, clientId, target.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(target.IsPrimary);
        Assert.Equal(1, repo.SaveChangesCallCount);
    }

    [Fact]
    public async Task Target_not_found_is_not_found()
    {
        var clientId = Guid.NewGuid();
        var repo = new InMemoryClientContactRepository();
        repo.Add(Contact(clientId, "Ada", isPrimary: true));
        var handler = new SetPrimaryClientContactCommandHandler(repo);

        var result = await handler.Handle(
            new SetPrimaryClientContactCommand(TestClients.TenantId, clientId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientContactErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Setting_the_already_primary_contact_is_a_no_op()
    {
        var clientId = Guid.NewGuid();
        var target = Contact(clientId, "Ada", isPrimary: true);
        var repo = new InMemoryClientContactRepository();
        repo.Add(target);
        target.ClearDomainEvents();
        var handler = new SetPrimaryClientContactCommandHandler(repo);

        var result = await handler.Handle(
            new SetPrimaryClientContactCommand(TestClients.TenantId, clientId, target.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(target.IsPrimary);
        Assert.Empty(target.DomainEvents);      // no write, no event
        Assert.Equal(0, repo.SaveChangesCallCount);
    }
}
