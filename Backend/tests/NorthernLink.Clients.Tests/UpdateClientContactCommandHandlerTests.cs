using NorthernLink.Clients.Application.ClientContacts.Update;
using NorthernLink.Clients.Domain.ClientContacts;
using Xunit;

namespace NorthernLink.Clients.Tests;

/// <summary>
/// The contact update handler's gates (missing contact, wrong client, primary uniqueness)
/// and the happy path through to persistence.
/// </summary>
public class UpdateClientContactCommandHandlerTests
{
    private static readonly Guid ClientId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

    private readonly InMemoryClientContactRepository _contacts = new();

    private UpdateClientContactCommandHandler Handler() => new(_contacts);

    private ClientContact AddContact(
        Guid? clientId = null,
        string name = "Dana Reyes",
        string email = "dana@example.com",
        bool isPrimary = false)
    {
        var result = ClientContact.Create(
            TestClients.TenantId, clientId ?? ClientId, name, "Logistics Lead",
            email, null, null, isPrimary);
        Assert.True(result.IsSuccess);
        _contacts.Add(result.Value);
        return result.Value;
    }

    private static UpdateClientContactCommand Command(
        Guid contactId,
        Guid? clientId = null,
        string name = "Dana Reyes",
        bool isPrimary = false,
        bool receivesEmailReports = false,
        bool receivesAccrualsReports = false) => new(
        TestClients.TenantId,
        clientId ?? ClientId,
        contactId,
        name,
        "Logistics Lead",
        "dana@example.com",
        null,
        null,
        isPrimary,
        receivesEmailReports,
        receivesAccrualsReports);

    [Fact]
    public async Task Unknown_contact_returns_NotFound()
    {
        var result = await Handler().Handle(Command(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientContactErrors.NotFound, result.Error);
        Assert.Equal(0, _contacts.SaveChangesCallCount);
    }

    [Fact]
    public async Task Contact_belonging_to_a_different_client_returns_NotFound()
    {
        var contact = AddContact(clientId: Guid.NewGuid());

        var result = await Handler().Handle(Command(contact.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientContactErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Promoting_to_primary_when_another_primary_exists_is_a_conflict()
    {
        AddContact(name: "Sam Okoye", email: "sam@example.com", isPrimary: true);
        var contact = AddContact();

        var result = await Handler().Handle(Command(contact.Id, isPrimary: true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientContactErrors.PrimaryAlreadyExists, result.Error);
        Assert.False(contact.IsPrimary); // nothing changed
    }

    [Fact]
    public async Task Resaving_the_existing_primary_contact_as_primary_succeeds()
    {
        var contact = AddContact(isPrimary: true);

        var result = await Handler().Handle(Command(contact.Id, isPrimary: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(contact.IsPrimary);
        Assert.Equal(1, _contacts.SaveChangesCallCount);
    }

    [Fact]
    public async Task Validation_failure_from_the_aggregate_is_returned_and_nothing_is_saved()
    {
        var contact = AddContact();

        var result = await Handler().Handle(Command(contact.Id, name: "  "), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientContactErrors.NameRequired, result.Error);
        Assert.Equal(0, _contacts.SaveChangesCallCount);
    }

    [Fact]
    public async Task Happy_path_updates_the_flags_and_saves()
    {
        var contact = AddContact();

        var result = await Handler().Handle(
            Command(contact.Id, receivesEmailReports: true, receivesAccrualsReports: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(contact.ReceivesEmailReports);
        Assert.True(contact.ReceivesAccrualsReports);
        Assert.Equal(1, _contacts.SaveChangesCallCount);
    }
}
