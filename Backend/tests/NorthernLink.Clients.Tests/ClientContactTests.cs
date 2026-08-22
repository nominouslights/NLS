using NorthernLink.Clients.Application.ClientContacts;
using NorthernLink.Clients.Domain.ClientContacts;
using NorthernLink.Clients.Domain.ClientContacts.Events;
using Xunit;

namespace NorthernLink.Clients.Tests;

/// <summary>
/// ClientContact factory validation and the ReceivesEmailReports flag (the pickup-email-report
/// opt-in — mirrors IsPrimary but with NO uniqueness rule): it must round-trip onto the
/// aggregate and onto the public <see cref="ClientContactResponse"/>, and default to false.
/// </summary>
public class ClientContactTests
{
    private static readonly Guid ClientId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

    [Fact]
    public void Name_is_required()
    {
        var result = ClientContact.Create(
            TestClients.TenantId, ClientId, "  ", "Logistics Lead", "a@example.com", null, null, isPrimary: false);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientContactErrors.NameRequired, result.Error);
    }

    [Fact]
    public void Title_is_required()
    {
        var result = ClientContact.Create(
            TestClients.TenantId, ClientId, "Dana Reyes", "  ", "a@example.com", null, null, isPrimary: false);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientContactErrors.TitleRequired, result.Error);
    }

    [Fact]
    public void Create_trims_fields_and_raises_created_event()
    {
        var result = ClientContact.Create(
            TestClients.TenantId, ClientId, "  Dana Reyes  ", "  Logistics Lead  ",
            "  dana@example.com  ", "  ", "  ", isPrimary: true);

        Assert.True(result.IsSuccess);
        var contact = result.Value;
        Assert.Equal("Dana Reyes", contact.Name);
        Assert.Equal("Logistics Lead", contact.Title);
        Assert.Equal("dana@example.com", contact.Email);
        Assert.Null(contact.Phone); // whitespace-only normalizes to null
        Assert.Null(contact.Notes);
        Assert.True(contact.IsPrimary);
        var created = Assert.IsType<ClientContactCreatedDomainEvent>(Assert.Single(contact.DomainEvents));
        Assert.Equal(contact.Id, created.ContactId);
        Assert.Equal(ClientId, created.ClientId);
        Assert.Equal(TestClients.TenantId, created.TenantId);
    }

    [Fact]
    public void ReceivesEmailReports_true_round_trips_onto_the_aggregate()
    {
        var result = ClientContact.Create(
            TestClients.TenantId, ClientId, "Dana Reyes", "Logistics Lead",
            "dana@example.com", null, null, isPrimary: false, receivesEmailReports: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.ReceivesEmailReports);
    }

    [Fact]
    public void ReceivesEmailReports_defaults_to_false_when_omitted()
    {
        var result = ClientContact.Create(
            TestClients.TenantId, ClientId, "Dana Reyes", "Logistics Lead",
            "dana@example.com", null, null, isPrimary: false);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.ReceivesEmailReports);
    }

    [Fact]
    public void ReceivesEmailReports_can_be_explicitly_false()
    {
        var result = ClientContact.Create(
            TestClients.TenantId, ClientId, "Dana Reyes", "Logistics Lead",
            "dana@example.com", null, null, isPrimary: true, receivesEmailReports: false);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.ReceivesEmailReports);
    }

    [Fact]
    public void ReceivesEmailReports_is_independent_of_IsPrimary_no_uniqueness_rule()
    {
        // Unlike IsPrimary, ReceivesEmailReports carries no "only one per client" constraint —
        // the aggregate happily creates two report-receiving, non-primary contacts.
        var first = ClientContact.Create(
            TestClients.TenantId, ClientId, "Dana Reyes", "Logistics Lead",
            "dana@example.com", null, null, isPrimary: false, receivesEmailReports: true);
        var second = ClientContact.Create(
            TestClients.TenantId, ClientId, "Sam Okoye", "Site Manager",
            "sam@example.com", null, null, isPrimary: false, receivesEmailReports: true);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(first.Value.ReceivesEmailReports);
        Assert.True(second.Value.ReceivesEmailReports);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClientContactResponse_carries_ReceivesEmailReports(bool receivesEmailReports)
    {
        var now = DateTimeOffset.UtcNow;
        var response = new ClientContactResponse(
            Guid.NewGuid(),
            ClientId,
            "Dana Reyes",
            "Logistics Lead",
            "dana@example.com",
            null,
            null,
            IsPrimary: false,
            ReceivesEmailReports: receivesEmailReports,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);

        Assert.Equal(receivesEmailReports, response.ReceivesEmailReports);
    }
}
