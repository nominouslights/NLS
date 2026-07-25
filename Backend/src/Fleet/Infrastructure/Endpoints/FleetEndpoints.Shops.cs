using Microsoft.AspNetCore.Http;
using NorthernLink.Fleet.Application.Shops.GetShops;
using NorthernLink.Fleet.Application.Shops.Register;
using NorthernLink.Fleet.Application.Shops.Update;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Fleet.Infrastructure.Endpoints;

/// <summary>Shop / parts-partner endpoints — fleet-wide reference data reused on work orders.</summary>
public static partial class FleetEndpoints
{
    private static async Task<IResult> GetShops(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetShopsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RegisterShop(
        ShopRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new RegisterShopCommand(
            tenantId,
            request.Name ?? string.Empty,
            request.ContactName,
            request.Phone,
            request.Email,
            request.Address,
            request.GstBusinessNo,
            request.MpiAccredited,
            request.InspectionStationNo,
            request.SuppliesParts,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/fleet/shops/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateShop(
        Guid id, ShopRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateShopCommand(
            tenantId,
            id,
            request.Name ?? string.Empty,
            request.ContactName,
            request.Phone,
            request.Email,
            request.Address,
            request.GstBusinessNo,
            request.MpiAccredited,
            request.InspectionStationNo,
            request.SuppliesParts,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }
}

/// <summary>Request body for POST/PUT /api/fleet/shops.</summary>
public sealed record ShopRequest(
    string? Name,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    string? GstBusinessNo,
    bool MpiAccredited,
    string? InspectionStationNo,
    bool SuppliesParts,
    string? Notes);
