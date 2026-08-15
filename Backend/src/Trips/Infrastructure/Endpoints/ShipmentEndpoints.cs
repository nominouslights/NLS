using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Shipments;
using NorthernLink.Trips.Application.Shipments.AddLeg;
using NorthernLink.Trips.Application.Shipments.BulkAssign;
using NorthernLink.Trips.Application.Shipments.Cancel;
using NorthernLink.Trips.Application.Shipments.CloseWithoutBilling;
using NorthernLink.Trips.Application.Shipments.GetById;
using NorthernLink.Trips.Application.Shipments.GetShipments;
using NorthernLink.Trips.Application.Shipments.RecordDelivery;
using NorthernLink.Trips.Application.Shipments.RecordLegDrop;
using NorthernLink.Trips.Application.Shipments.RecordLegPickup;
using NorthernLink.Trips.Application.Shipments.Register;
using NorthernLink.Trips.Application.Shipments.RemoveLeg;
using NorthernLink.Trips.Application.Shipments.SetBilling;
using NorthernLink.Trips.Application.Shipments.SetSecured;
using NorthernLink.Trips.Application.Shipments.Update;
using NorthernLink.Trips.Domain.Shipments;

namespace NorthernLink.Trips.Infrastructure.Endpoints;

/// <summary>
/// Cargo under <c>/api/trips/shipments</c>. Shipments live in the Trips library, so the route is
/// owned by <see cref="TripsEndpoints"/> — every other Trips aggregate already sits under
/// <c>/api/trips/…</c> (routes, stops, manifests, schedule-templates), and the <c>{id:guid}</c>
/// constraints on the trip item routes keep the literal <c>shipments</c> segment unambiguous.
/// <para>
/// Every endpoint resolves the ambient tenant (401 when absent — the API half of dual tenant
/// enforcement), then dispatches via <see cref="ISender"/>. The tenant scopes the read side
/// through the DbContext query filter and Postgres RLS underneath it.
/// </para>
/// </summary>
public static class ShipmentEndpoints
{
    public static IEndpointRouteBuilder MapShipmentEndpoints(this IEndpointRouteBuilder app)
    {
        var shipments = app.MapGroup("/api/trips/shipments").RequireAuthorization();

        shipments.MapGet("", GetShipments);
        shipments.MapGet("{id:guid}", GetShipmentById);
        shipments.MapPost("", RegisterShipment);
        shipments.MapPut("{id:guid}", UpdateShipment);

        // Routing. A shipment rides one leg per trip; two legs is a hub transfer.
        shipments.MapPost("{id:guid}/legs", AddLeg);
        shipments.MapDelete("{id:guid}/legs/{sequence:int}", RemoveLeg);
        shipments.MapPost("{id:guid}/legs/{sequence:int}/pickup", RecordLegPickup);
        shipments.MapPost("{id:guid}/legs/{sequence:int}/drop", RecordLegDrop);
        shipments.MapPost("bulk-assign", BulkAssign);

        shipments.MapPost("{id:guid}/deliver", RecordDelivery);
        shipments.MapPost("{id:guid}/secured", SetSecured);
        shipments.MapPut("{id:guid}/billing", SetBilling);
        shipments.MapPost("{id:guid}/cancel", CancelShipment);
        shipments.MapPost("{id:guid}/close-without-billing", CloseWithoutBilling);

        return app;
    }

    private static async Task<IResult> GetShipments(
        string? status,
        string? kind,
        bool? unassignedOnly,
        Guid? tripId,
        string? tripNumber,
        Guid? clientId,
        bool? clientless,
        bool? awaitingDelivery,
        bool? awaitingTransfer,
        bool? billable,
        DateOnly? from,
        DateOnly? to,
        string? q,
        int? page,
        int? pageSize,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var filter = new ShipmentListFilter
        {
            Status = status,
            Kind = kind,
            UnassignedOnly = unassignedOnly ?? false,
            TripId = tripId,
            TripNumber = tripNumber,
            ClientId = clientId,
            ClientlessOnly = clientless ?? false,
            AwaitingDelivery = awaitingDelivery ?? false,
            AwaitingTransferOnly = awaitingTransfer ?? false,
            BillableOnly = billable ?? false,
            From = from,
            To = to,
            Query = q,
            Page = page,
            PageSize = pageSize,
        };

        var result = await sender.Query(new GetShipmentsQuery(filter), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetShipmentById(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetShipmentByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RegisterShipment(
        RegisterShipmentRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new RegisterShipmentCommand(
            tenantId, request.ToDetails(), request.Source, request.EnteredBy);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/trips/shipments/{result.Value}", new ShipmentCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateShipment(
        Guid id,
        RegisterShipmentRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateShipmentCommand(id, request.ToDetails(), request.Source, request.EnteredBy);
        return await SendCommand(sender, command, cancellationToken);
    }

    private static async Task<IResult> AddLeg(
        Guid id,
        AddShipmentLegRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        // Deliberately carries no client field: a leg says which run moves the goods, never who
        // pays for them.
        var command = new AddShipmentLegCommand(
            id, request.TripId, request.FromStopId, request.FromName, request.ToStopId, request.ToName);

        return await SendCommand(sender, command, cancellationToken);
    }

    private static async Task<IResult> RemoveLeg(
        Guid id, int sequence, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        return await SendCommand(sender, new RemoveShipmentLegCommand(id, sequence), cancellationToken);
    }

    private static async Task<IResult> RecordLegPickup(
        Guid id,
        int sequence,
        LegEventRequest? request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var command = new RecordShipmentLegPickupCommand(id, sequence, request?.AtUtc, request?.By);
        return await SendCommand(sender, command, cancellationToken);
    }

    private static async Task<IResult> RecordLegDrop(
        Guid id,
        int sequence,
        LegEventRequest? request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var command = new RecordShipmentLegDropCommand(id, sequence, request?.AtUtc, request?.By);
        return await SendCommand(sender, command, cancellationToken);
    }

    private static async Task<IResult> BulkAssign(
        BulkAssignShipmentsRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var command = new BulkAssignShipmentsCommand(request.TripId, request.ShipmentIds);
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RecordDelivery(
        Guid id,
        DeliverShipmentRequest? request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var command = new RecordShipmentDeliveryCommand(
            id, request?.AtUtc, request?.ReceivedBy, request?.Note);

        return await SendCommand(sender, command, cancellationToken);
    }

    private static async Task<IResult> SetSecured(
        Guid id,
        SetSecuredRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        return await SendCommand(sender, new SetShipmentSecuredCommand(id, request.Secured), cancellationToken);
    }

    private static async Task<IResult> SetBilling(
        Guid id,
        SetShipmentBillingRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var command = new SetShipmentBillingCommand(
            id, request.ClientId, request.PoNumber, request.ChargeCad, request.PaymentMethod);

        return await SendCommand(sender, command, cancellationToken);
    }

    private static async Task<IResult> CancelShipment(
        Guid id,
        ReasonRequest? request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        return await SendCommand(sender, new CancelShipmentCommand(id, request?.Reason), cancellationToken);
    }

    private static async Task<IResult> CloseWithoutBilling(
        Guid id,
        ReasonRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var command = new CloseShipmentWithoutBillingCommand(id, request.Reason ?? string.Empty);
        return await SendCommand(sender, command, cancellationToken);
    }

    private static async Task<IResult> SendCommand(
        ISender sender, ICommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }
}

/// <summary>
/// Register/update payload. <see cref="ClientId"/> is the shipment's own payer — the client name
/// is never accepted from the caller, it is snapshotted from the client_lookup replica, so a
/// mistyped name can never disagree with the id it claims to describe.
/// </summary>
public sealed record RegisterShipmentRequest(
    string Description,
    ShipmentKind? Kind,
    int? Pieces,
    decimal? WeightKg,
    decimal? LengthCm,
    decimal? WidthCm,
    decimal? HeightCm,
    bool? Hazmat,
    decimal? DeclaredValueCad,
    string? SpecialHandling,
    string? ConsignorName,
    string? ConsignorContact,
    string? ConsigneeName,
    string? ConsigneeContact,
    Guid? OriginStopId,
    string? OriginName,
    Guid? DestinationStopId,
    string? DestinationName,
    DateOnly? ReadyDate,
    DateOnly? RequiredByDate,
    Guid? ClientId,
    string? PoNumber,
    decimal? ChargeCad,
    ShipmentPaymentMethod? PaymentMethod,
    ShipmentSource Source,
    string? EnteredBy)
{
    public ShipmentDetails ToDetails() => new()
    {
        Description = Description,
        Kind = Kind ?? ShipmentKind.Parcel,
        Pieces = Pieces ?? 1,
        WeightKg = WeightKg,
        LengthCm = LengthCm,
        WidthCm = WidthCm,
        HeightCm = HeightCm,
        Hazmat = Hazmat ?? false,
        DeclaredValueCad = DeclaredValueCad,
        SpecialHandling = SpecialHandling,
        ConsignorName = ConsignorName,
        ConsignorContact = ConsignorContact,
        ConsigneeName = ConsigneeName,
        ConsigneeContact = ConsigneeContact,
        OriginStopId = OriginStopId,
        OriginName = OriginName,
        DestinationStopId = DestinationStopId,
        DestinationName = DestinationName,
        ReadyDate = ReadyDate,
        RequiredByDate = RequiredByDate,
        ClientId = ClientId,

        // Placeholder only — the handler overwrites it from client_lookup, and the domain
        // rejects a client id with no name, so this can never survive as-is.
        ClientName = ClientId is null ? null : string.Empty,
        PoNumber = PoNumber,
        ChargeCad = ChargeCad,
        PaymentMethod = PaymentMethod,
    };
}

public sealed record AddShipmentLegRequest(
    Guid TripId,
    Guid? FromStopId,
    string? FromName,
    Guid? ToStopId,
    string? ToName);

public sealed record BulkAssignShipmentsRequest(Guid TripId, IReadOnlyList<Guid> ShipmentIds);

public sealed record LegEventRequest(DateTimeOffset? AtUtc, string? By);

public sealed record DeliverShipmentRequest(DateTimeOffset? AtUtc, string? ReceivedBy, string? Note);

public sealed record SetSecuredRequest(bool Secured);

public sealed record SetShipmentBillingRequest(
    Guid? ClientId,
    string? PoNumber,
    decimal? ChargeCad,
    ShipmentPaymentMethod? PaymentMethod);

public sealed record ReasonRequest(string? Reason);

public sealed record ShipmentCreatedResponse(Guid Id);
