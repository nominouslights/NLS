using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Application.Vendors.Create;

/// <summary>
/// Handles <see cref="CreateVendorCommand"/>: domain validation first, then the case-insensitive
/// name check (<see cref="VendorNameRule"/>), then one save. A malformed payload reports its
/// validation error, never a conflict.
/// </summary>
public sealed class CreateVendorCommandHandler(IVendorRepository repository)
    : ICommandHandler<CreateVendorCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateVendorCommand command, CancellationToken cancellationToken)
    {
        var vendorResult = Vendor.Create(command.TenantId, command.Details, command.ActorId);
        if (vendorResult.IsFailure)
        {
            return Result.Failure<Guid>(vendorResult.Error);
        }

        var vendor = vendorResult.Value;

        var unique = await VendorNameRule.EnsureUniqueAsync(repository, vendor.Name, selfId: null, cancellationToken);
        if (unique.IsFailure)
        {
            return Result.Failure<Guid>(unique.Error);
        }

        repository.Add(vendor);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(vendor.Id);
    }
}
