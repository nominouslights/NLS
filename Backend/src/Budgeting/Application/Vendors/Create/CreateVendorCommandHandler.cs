using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Application.Vendors.Create;

/// <summary>
/// Handles <see cref="CreateVendorCommand"/>: domain validation first, then the case-insensitive
/// name check (<see cref="VendorNameRule"/>), then one save — whose
/// unique-index race is also a 409, never a 500. A malformed payload reports its
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

        // The check above is not atomic with the insert: a concurrent create of the same
        // normalized name can pass it too, and the unique index rejects the loser here.
        if (!await repository.TrySaveChangesAsync(cancellationToken))
        {
            return Result.Failure<Guid>(await VendorNameRule.ConflictAfterLostRaceAsync(
                repository, vendor.Name, selfId: null, cancellationToken));
        }

        return Result.Success(vendor.Id);
    }
}
