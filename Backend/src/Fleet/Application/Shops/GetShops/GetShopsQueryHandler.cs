using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Shops;

namespace NorthernLink.Fleet.Application.Shops.GetShops;

public sealed class GetShopsQueryHandler(IShopReadService readService)
    : IQueryHandler<GetShopsQuery, IReadOnlyList<ShopResponse>>
{
    public async Task<Result<IReadOnlyList<ShopResponse>>> Handle(
        GetShopsQuery query,
        CancellationToken cancellationToken)
    {
        var shops = await readService.GetShopsAsync(cancellationToken);
        return Result.Success(shops);
    }
}
