using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Shops;

namespace NorthernLink.Fleet.Application.Shops.GetShops;

/// <summary>Lists every shop / parts partner visible to the current tenant.</summary>
public sealed record GetShopsQuery(Guid TenantId) : IQuery<IReadOnlyList<ShopResponse>>;
