using NorthernLink.Clients.Application.ClientInteractions;
using NorthernLink.Clients.Application.ClientInteractions.Create;
using NorthernLink.Clients.Application.ClientInteractions.Delete;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.ClientInteractions;
using NorthernLink.Clients.Domain.ClientInteractions.Events;
using Xunit;

namespace NorthernLink.Clients.Tests;

/// <summary>ClientInteraction factory validation, create/delete handlers, and wire-string mapping.</summary>
public class ClientInteractionTests
{
    private static NorthernLink.Shared.Kernel.Result<ClientInteraction> Create(
        string summary = "Discussed Q2 crew rotation",
        InteractionType type = InteractionType.Meeting,
        IEnumerable<Guid>? participants = null,
        DateOnly? followUpDate = null,
        string? followUpNote = null) =>
        ClientInteraction.Create(
            TestClients.TenantId,
            Guid.NewGuid(),
            type,
            new DateOnly(2026, 3, 10),
            summary,
            participants,
            followUpDate,
            followUpNote);

    [Fact]
    public void Summary_is_required()
    {
        var result = Create(summary: "   ");

        Assert.True(result.IsFailure);
        Assert.Equal(ClientInteractionErrors.SummaryRequired, result.Error);
    }

    [Fact]
    public void Create_trims_summary_defaults_empty_participants_and_raises_created_event()
    {
        var result = Create(summary: "  Site walk-through  ", participants: null);

        Assert.True(result.IsSuccess);
        var interaction = result.Value;
        Assert.Equal("Site walk-through", interaction.Summary);
        Assert.Empty(interaction.ParticipantContactIds);
        var created = Assert.IsType<ClientInteractionCreatedDomainEvent>(Assert.Single(interaction.DomainEvents));
        Assert.Equal(interaction.Id, created.InteractionId);
        Assert.Equal(TestClients.TenantId, created.TenantId);
    }

    [Fact]
    public void Create_keeps_participant_ids_and_follow_up()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var interaction = Create(
            participants: [p1, p2],
            followUpDate: new DateOnly(2026, 4, 1),
            followUpNote: "Send updated quote").Value;

        Assert.Equal([p1, p2], interaction.ParticipantContactIds);
        Assert.Equal(new DateOnly(2026, 4, 1), interaction.FollowUpDate);
        Assert.Equal("Send updated quote", interaction.FollowUpNote);
    }

    [Theory]
    [InlineData(InteractionType.Call, "Call")]
    [InlineData(InteractionType.SiteVisit, "Site Visit")]
    [InlineData(InteractionType.Other, "Other")]
    public void Wire_mapping_round_trips(InteractionType type, string wire)
    {
        Assert.Equal(wire, InteractionTypeWire.ToWire(type));
        Assert.Equal(type, InteractionTypeWire.FromWire(wire));
    }

    [Fact]
    public void Wire_mapping_accepts_the_enum_name_and_falls_back_to_other()
    {
        Assert.Equal(InteractionType.SiteVisit, InteractionTypeWire.FromWire("SiteVisit"));
        Assert.Equal(InteractionType.Other, InteractionTypeWire.FromWire(null));
        Assert.Equal(InteractionType.Other, InteractionTypeWire.FromWire("nonsense"));
    }

    [Fact]
    public async Task Creating_an_interaction_for_an_unknown_client_is_not_found()
    {
        var clients = new InMemoryClientRepository();
        var interactions = new InMemoryClientInteractionRepository();
        var handler = new CreateClientInteractionCommandHandler(clients, interactions);

        var command = new CreateClientInteractionCommand(
            TestClients.TenantId, Guid.NewGuid(), InteractionType.Call, new DateOnly(2026, 3, 10),
            "Follow-up call", [], null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientErrors.NotFound, result.Error);
        Assert.Empty(interactions.Interactions);
    }

    [Fact]
    public async Task Creating_an_interaction_persists_it_and_returns_its_id()
    {
        var clients = new InMemoryClientRepository();
        var client = TestClients.Create();
        clients.Add(client);
        var interactions = new InMemoryClientInteractionRepository();
        var handler = new CreateClientInteractionCommandHandler(clients, interactions);

        var command = new CreateClientInteractionCommand(
            TestClients.TenantId, client.Id, InteractionType.SiteVisit, new DateOnly(2026, 3, 10),
            "Toured the Thompson depot", [Guid.NewGuid()], new DateOnly(2026, 3, 24), "Confirm dock hours");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var interaction = Assert.Single(interactions.Interactions);
        Assert.Equal(result.Value, interaction.Id);
        Assert.Equal(client.Id, interaction.ClientId);
        Assert.Equal(InteractionType.SiteVisit, interaction.Type);
        Assert.Equal(1, interactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task Deleting_an_interaction_removes_it()
    {
        var interactions = new InMemoryClientInteractionRepository();
        var interaction = Create().Value;
        interactions.Add(interaction);
        var handler = new DeleteClientInteractionCommandHandler(interactions);

        var result = await handler.Handle(
            new DeleteClientInteractionCommand(TestClients.TenantId, interaction.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(interactions.Interactions);
    }

    [Fact]
    public async Task Deleting_an_unknown_interaction_is_not_found()
    {
        var handler = new DeleteClientInteractionCommandHandler(new InMemoryClientInteractionRepository());

        var result = await handler.Handle(
            new DeleteClientInteractionCommand(TestClients.TenantId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientInteractionErrors.NotFound, result.Error);
    }
}
