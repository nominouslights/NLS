using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Shops;

namespace NorthernLink.Fleet.Application.Shops.Update;

public sealed class UpdateShopCommandHandler(IShopRepository repository)
    : ICommandHandler<UpdateShopCommand>
{
    public async Task<Result> Handle(UpdateShopCommand command, CancellationToken cancellationToken)
    {
        var shop = await repository.GetByIdAsync(command.ShopId, cancellationToken);
        if (shop is null)
        {
            return Result.Failure(ShopErrors.NotFound);
        }

        var result = shop.UpdateDetails(
            command.Name,
            command.ContactName,
            command.Phone,
            command.Email,
            command.Address,
            command.GstBusinessNo,
            command.MpiAccredited,
            command.InspectionStationNo,
            command.SuppliesParts,
            command.Notes);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
