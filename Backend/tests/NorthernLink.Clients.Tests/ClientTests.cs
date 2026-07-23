using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.Clients.Events;
using Xunit;

namespace NorthernLink.Clients.Tests;

/// <summary>Client factory validation and update behavior.</summary>
public class ClientTests
{
    [Fact]
    public void Name_is_required()
    {
        var result = Client.Create(TestClients.TenantId, "  ", ClientType.Client, ClientServiceType.Charter, "Charter", null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientErrors.NameRequired, result.Error);
    }

    [Fact]
    public void Tag_is_required()
    {
        var result = Client.Create(TestClients.TenantId, "Hudbay", ClientType.Client, ClientServiceType.ContractCrew, "", null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientErrors.TagRequired, result.Error);
    }

    [Fact]
    public void ServiceType_required_for_Client_type()
    {
        var result = Client.Create(TestClients.TenantId, "Hudbay", ClientType.Client, null, "Corporate", null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientErrors.ServiceTypeRequired, result.Error);
    }

    [Fact]
    public void ServiceType_forbidden_for_VendorPartner_type()
    {
        var result = Client.Create(TestClients.TenantId, "Miller Mover", ClientType.VendorPartner, ClientServiceType.Charter, "Vendor / Partner", null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientErrors.ServiceTypeNotAllowed, result.Error);
    }

    [Fact]
    public void Create_trims_fields_and_raises_created_event()
    {
        var result = Client.Create(
            TestClients.TenantId, "  Hudbay Minerals  ", ClientType.Client, ClientServiceType.ContractCrew, " Corporate ", null, "  ");

        Assert.True(result.IsSuccess);
        var client = result.Value;
        Assert.Equal("Hudbay Minerals", client.Name);
        Assert.Equal(ClientType.Client, client.Type);
        Assert.Equal("Corporate", client.Tag);
        Assert.Null(client.AgreementReference);
        Assert.Null(client.Notes); // whitespace-only notes normalize to null
        var created = Assert.IsType<ClientCreatedDomainEvent>(Assert.Single(client.DomainEvents));
        Assert.Equal(client.Id, created.ClientId);
        Assert.Equal(TestClients.TenantId, created.TenantId);
    }

    [Fact]
    public void Update_applies_changes_and_raises_updated_event()
    {
        var client = TestClients.Create();
        client.ClearDomainEvents();

        var result = client.Update("Vale Base Metals", ClientServiceType.Cargo, "Vendor / Partner", null, "Renamed 2026");

        Assert.True(result.IsSuccess);
        Assert.Equal("Vale Base Metals", client.Name);
        Assert.Equal(ClientServiceType.Cargo, client.ServiceType);
        Assert.Equal("Vendor / Partner", client.Tag);
        Assert.Null(client.AgreementReference);
        Assert.Equal("Renamed 2026", client.Notes);
        Assert.IsType<ClientUpdatedDomainEvent>(Assert.Single(client.DomainEvents));
    }

    [Fact]
    public void Update_with_blank_name_is_rejected_and_state_unchanged()
    {
        var client = TestClients.Create(name: "Vale Manitoba Operations");

        var result = client.Update("", ClientServiceType.Charter, "Charter", null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientErrors.NameRequired, result.Error);
        Assert.Equal("Vale Manitoba Operations", client.Name);
    }
}
