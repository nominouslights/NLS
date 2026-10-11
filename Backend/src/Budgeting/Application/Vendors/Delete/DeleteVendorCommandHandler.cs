using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Application.Vendors.Delete;

/// <summary>
/// Handles <see cref="DeleteVendorCommand"/>: the vendor must exist (in this tenant), and
/// <see cref="IVendorUsageProbe"/> must say no budget item carries a share for it — otherwise 409
/// <c>Budgeting.Vendor.InUse</c>, and nothing is removed. The aggregate raises no event on delete:
/// the audit pipeline exempts deletes and writes the synthetic <c>aggregate-deleted</c> journal
/// row that drives the projection to drop the read row.
/// <para>
/// <b>The probe and the delete are atomic with respect to new shares.</b> Everything from reading
/// the vendor onwards runs under the tenant's vendor register lock
/// (<see cref="IVendorRepository.LockRegisterAsync"/>), committed with the delete; every item write
/// that adds a vendor share takes the same lock before its register lookup
/// (<see cref="Allocations.BudgetItemVendorRule"/>, and the items copy). Whichever request gets the
/// lock first wins and the other sees its outcome: the item commits first → this delete reports
/// InUse; this delete commits first → the item's lookup reports VendorNotFound.
/// </para>
/// </summary>
public sealed class DeleteVendorCommandHandler(IVendorRepository repository, IVendorUsageProbe usageProbe)
    : ICommandHandler<DeleteVendorCommand>
{
    public async Task<Result> Handle(DeleteVendorCommand command, CancellationToken cancellationToken)
    {
        await using var registerLock = await repository.LockRegisterAsync(command.TenantId, cancellationToken);

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
        await registerLock.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
