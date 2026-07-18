using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Shops;

namespace NorthernLink.Fleet.Application.Shops.Register;

public sealed class RegisterShopCommandHandler(IShopRepository repository)
    : ICommandHandler<RegisterShopCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterShopCommand command, CancellationToken cancellationToken)
    {
        var sequence = await repository.NextSequenceAsync(command.TenantId, cancellationToken);
        var number = $"SHOP-{sequence:00}";

        var shopResult = Shop.Register(
            command.TenantId,
            number,
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

        if (shopResult.IsFailure)
        {
            return Result.Failure<Guid>(shopResult.Error);
        }

        repository.Add(shopResult.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(shopResult.Value.Id);
    }
}
