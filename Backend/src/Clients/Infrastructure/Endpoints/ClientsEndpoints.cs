using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Clients.Application.Clients.Create;
using NorthernLink.Clients.Application.Clients.GetClientById;
using NorthernLink.Clients.Application.Clients.GetClients;
using NorthernLink.Clients.Application.Clients.Update;
using NorthernLink.Clients.Application.ClientContacts.Create;
using NorthernLink.Clients.Application.ClientContacts.GetForClient;
using NorthernLink.Clients.Application.ClientContacts.SetPrimary;
using NorthernLink.Clients.Application.ClientInteractions;
using NorthernLink.Clients.Application.ClientInteractions.Create;
using NorthernLink.Clients.Application.ClientInteractions.Delete;
using NorthernLink.Clients.Application.ClientInteractions.GetForClient;
using NorthernLink.Clients.Application.ClientInteractions.GetOpenFollowUps;
using NorthernLink.Clients.Application.Contracts.Create;
using NorthernLink.Clients.Application.Contracts.GetForClient;
using NorthernLink.Clients.Application.Contracts.Terminate;
using NorthernLink.Clients.Application.Contracts.Update;
using NorthernLink.Clients.Application.PurchaseOrders.Create;
using NorthernLink.Clients.Application.PurchaseOrders.Delete;
using NorthernLink.Clients.Application.PurchaseOrders.GetForClient;
using NorthernLink.Clients.Application.PurchaseOrders.Update;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.Contracts;

namespace NorthernLink.Clients.Infrastructure.Endpoints;

/// <summary>
/// The Clients module's minimal-API surface under <c>/api/clients</c>. Every endpoint
/// resolves the ambient tenant (401 when absent — the API half of dual tenant
/// enforcement), stamps it onto the command/query, and dispatches via <see cref="ISender"/>.
/// </summary>
public static class ClientsEndpoints
{
    public static IEndpointRouteBuilder MapClientsEndpoints(this IEndpointRouteBuilder app)
    {
        var clients = app.MapGroup("/api/clients").RequireAuthorization();

        clients.MapGet("", GetClients);
        clients.MapPost("", CreateClient);
        clients.MapGet("{id:guid}", GetClientById);
        clients.MapPut("{id:guid}", UpdateClient);

        // Contracts — nested under their client.
        clients.MapGet("{id:guid}/contracts", GetClientContracts);
        clients.MapPost("{id:guid}/contracts", CreateContract);
        clients.MapPut("{id:guid}/contracts/{contractId:guid}", UpdateContract);
        clients.MapPost("{id:guid}/contracts/{contractId:guid}/terminate", TerminateContract);

        // Contacts — nested under their client.
        clients.MapGet("{id:guid}/contacts", GetClientContacts);
        clients.MapPost("{id:guid}/contacts", CreateClientContact);
        clients.MapPut("{id:guid}/contacts/{contactId:guid}/primary", SetPrimaryClientContact);

        // Interactions — nested under their client, plus a cross-client open-follow-ups feed.
        // "follow-ups" is a literal segment; the {id:guid} constraint rejects it, so no collision.
        clients.MapGet("follow-ups", GetOpenFollowUps);
        clients.MapGet("{id:guid}/interactions", GetClientInteractions);
        clients.MapPost("{id:guid}/interactions", CreateClientInteraction);
        clients.MapDelete("{id:guid}/interactions/{interactionId:guid}", DeleteClientInteraction);

        // Purchase orders — nested under their client.
        clients.MapGet("{id:guid}/purchase-orders", GetClientPurchaseOrders);
        clients.MapPost("{id:guid}/purchase-orders", CreatePurchaseOrder);
        clients.MapPut("{id:guid}/purchase-orders/{poId:guid}", UpdatePurchaseOrder);
        clients.MapDelete("{id:guid}/purchase-orders/{poId:guid}", DeletePurchaseOrder);

        return app;
    }

    private static async Task<IResult> GetClients(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetClientsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetClientById(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetClientByIdQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateClient(
        ClientRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateClientCommand(
            tenantId,
            request.Name ?? string.Empty,
            request.Type,
            request.ServiceType,
            request.Tag ?? string.Empty,
            request.AgreementReference,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/clients/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateClient(
        Guid id, ClientRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateClientCommand(
            tenantId,
            id,
            request.Name ?? string.Empty,
            request.ServiceType,
            request.Tag ?? string.Empty,
            request.AgreementReference,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetClientContracts(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetClientContractsQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateContract(
        Guid id, ContractRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateContractCommand(
            tenantId,
            id,
            request.StartDate,
            request.EndDate,
            request.BillingModel,
            request.RatePerRoundTripCad,
            request.GstApplicable,
            request.BudgetCode,
            request.BillingFrequency,
            request.NetTermsDays ?? 30,
            request.DefaultPoNumber);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/clients/{id}/contracts/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateContract(
        Guid id, Guid contractId, ContractRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateContractCommand(
            tenantId,
            contractId,
            request.StartDate,
            request.EndDate,
            request.BillingModel,
            request.RatePerRoundTripCad,
            request.GstApplicable,
            request.BudgetCode,
            request.BillingFrequency,
            request.NetTermsDays ?? 30,
            request.DefaultPoNumber);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> TerminateContract(
        Guid id, Guid contractId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new TerminateContractCommand(tenantId, contractId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetClientContacts(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetClientContactsQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateClientContact(
        Guid id, ClientContactRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateClientContactCommand(
            tenantId,
            id,
            request.Name ?? string.Empty,
            request.Title ?? string.Empty,
            request.Email,
            request.Phone,
            request.Notes,
            request.IsPrimary);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/clients/{id}/contacts/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> SetPrimaryClientContact(
        Guid id, Guid contactId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new SetPrimaryClientContactCommand(tenantId, id, contactId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetClientInteractions(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetClientInteractionsQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetOpenFollowUps(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetOpenFollowUpsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateClientInteraction(
        Guid id, ClientInteractionRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateClientInteractionCommand(
            tenantId,
            id,
            InteractionTypeWire.FromWire(request.Type),
            request.OccurredOn ?? default,
            request.Summary ?? string.Empty,
            request.ParticipantContactIds ?? [],
            request.FollowUpDate,
            request.FollowUpNote);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/clients/{id}/interactions/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> DeleteClientInteraction(
        Guid id, Guid interactionId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new DeleteClientInteractionCommand(tenantId, interactionId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetClientPurchaseOrders(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetClientPurchaseOrdersQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreatePurchaseOrder(
        Guid id, PurchaseOrderRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreatePurchaseOrderCommand(
            tenantId,
            id,
            request.PoNumber ?? string.Empty,
            request.Issued,
            request.Expiry,
            request.AmountCad,
            request.Note);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/clients/{id}/purchase-orders/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdatePurchaseOrder(
        Guid id, Guid poId, PurchaseOrderRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new UpdatePurchaseOrderCommand(
            tenantId,
            poId,
            request.PoNumber ?? string.Empty,
            request.Issued,
            request.Expiry,
            request.AmountCad,
            request.Note);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> DeletePurchaseOrder(
        Guid id, Guid poId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new DeletePurchaseOrderCommand(tenantId, poId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }
}

/// <summary>Body of a successful create (201, with Location header).</summary>
public sealed record EntityCreatedResponse(Guid Id);

/// <summary>
/// Request body for POST/PUT /api/clients. Type is the enum name (Client or VendorPartner);
/// ServiceType is the enum name, e.g. "ContractCrew" (required for Client, forbidden for
/// VendorPartner); Tag is the CRM roster grouping ("Corporate", "Program", "Charter",
/// "Individuals", "Vendor / Partner").
/// </summary>
public sealed record ClientRequest(
    string? Name,
    ClientType Type,
    ClientServiceType? ServiceType,
    string? Tag,
    string? AgreementReference,
    string? Notes);

/// <summary>
/// Request body for POST/PUT /api/clients/{id}/contracts. A null EndDate means evergreen;
/// RatePerRoundTripCad is required iff BillingModel is "RoundTripRate"; NetTermsDays
/// defaults to 30.
/// </summary>
public sealed record ContractRequest(
    DateOnly StartDate,
    DateOnly? EndDate,
    BillingModel BillingModel,
    decimal? RatePerRoundTripCad,
    bool GstApplicable,
    string? BudgetCode,
    BillingFrequency BillingFrequency,
    int? NetTermsDays,
    string? DefaultPoNumber);

/// <summary>Request body for POST /api/clients/{id}/contacts.</summary>
public sealed record ClientContactRequest(
    string? Name,
    string? Title,
    string? Email,
    string? Phone,
    string? Notes,
    bool IsPrimary);

/// <summary>Request body for POST/PUT /api/clients/{id}/purchase-orders.</summary>
public sealed record PurchaseOrderRequest(
    string? PoNumber,
    DateOnly Issued,
    DateOnly? Expiry,
    decimal? AmountCad,
    string? Note);

/// <summary>
/// Request body for POST /api/clients/{id}/interactions. Type is the wire string — "Call",
/// "Meeting", "Email", "Site Visit" (with a space), "Other"; a blank or unrecognized value maps
/// to "Other". ParticipantContactIds references client_contacts rows and may be omitted/empty.
/// </summary>
public sealed record ClientInteractionRequest(
    string? Type,
    DateOnly? OccurredOn,
    string? Summary,
    Guid[]? ParticipantContactIds,
    DateOnly? FollowUpDate,
    string? FollowUpNote);
