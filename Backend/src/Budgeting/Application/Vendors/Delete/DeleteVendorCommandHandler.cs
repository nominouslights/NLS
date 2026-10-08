using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Application.Vendors.Delete;

/// <summary>
/// Handles <see cref="DeleteVendorCommand"/>: the vendor must exist (in this tenant), and
/// <see cref="IVendorUsageProbe"/> must say nothing references it — otherwise 409
/// <c>Budgeting.Vendor.InUse</c>, and nothing is removed. The aggregate raises no event on delete:
/// the audit pipeline exempts deletes and writes the synthetic <c>aggregate-deleted</c> journal
/// row that drives the projection to drop the read row.
/// </summary>
public sealed class DeleteVendorCommandHandler(IVendorRepository repository, IVendorUsageProbe usageProbe)
    : ICommandHandler<DeleteVendorCommand>
{
    public async Task<Result> Handle(DeleteVendorCommand command, CancellationToken cancellationToken)
    {
        var vendor = await repository.GetByIdAsync(command.VendorId, cancellationToken);
        if (vendor is null)
        {
            return Result.Failure(VendorErrors.NotFound);
        }

        if (await usageProbe.IsReferencedAsync(vendor.Id, cancellationToken))
        {
            return Result.Failure(VendorErrors.InUse);
        }

        repository.Remove(vendor);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
