using Microsoft.EntityFrameworkCore;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Application.PurchaseOrders;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Read side — queries clients.rm_purchase_orders and maps to the public contract.</summary>
internal sealed class PurchaseOrderReadService(ClientsDbContext context) : IPurchaseOrderReadService
{
    public async Task<IReadOnlyList<PurchaseOrderResponse>> GetForClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        var purchaseOrders = await context.PurchaseOrderReadModels
            .AsNoTracking()
            .Where(p => p.ClientId == clientId)
            .OrderByDescending(p => p.Issued)
            .ToListAsync(cancellationToken);

        return purchaseOrders.Select(p => new PurchaseOrderResponse(
            p.Id,
            p.ClientId,
            p.PoNumber,
            p.Issued,
            p.Expiry,
            p.AmountCad,
            p.Note,
            p.CreatedAtUtc,
            p.UpdatedAtUtc)).ToList();
    }
}
