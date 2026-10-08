using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Application.Vendors.SetActive;

/// <summary>
/// Handles <see cref="SetVendorActiveCommand"/>. Idempotent: asking for the state a vendor is
/// already in changes nothing and still succeeds.
/// </summary>
public sealed class SetVendorActiveCommandHandler(IVendorRepository repository)
    : ICommandHandler<SetVendorActiveCommand>
{
    public async Task<Result> Handle(SetVendorActiveCommand command, CancellationToken cancellationToken)
    {
        var vendor = await repository.GetByIdAsync(command.VendorId, cancellationToken);
        if (vendor is null)
        {
            return Result.Failure(VendorErrors.NotFound);
        }

        var result = vendor.SetActive(command.IsActive, command.ActorId);
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
