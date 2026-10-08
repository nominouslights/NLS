using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Application.Vendors.Update;

/// <summary>
/// Handles <see cref="UpdateVendorCommand"/>. Order: field validation, then the vendor (another
/// tenant's reads as NotFound), then the name check excluding the vendor itself — so renaming
/// "Acme fuel" to "Acme Fuel" is allowed while renaming it onto another vendor's name is a 409.
/// An edit that changes nothing raises no event and so writes nothing (see <see cref="Vendor.Update"/>).
/// </summary>
public sealed class UpdateVendorCommandHandler(IVendorRepository repository)
    : ICommandHandler<UpdateVendorCommand>
{
    public async Task<Result> Handle(UpdateVendorCommand command, CancellationToken cancellationToken)
    {
        var validation = Vendor.Validate(command.Details);
        if (validation.IsFailure)
        {
            return validation;
        }

        var vendor = await repository.GetByIdAsync(command.VendorId, cancellationToken);
        if (vendor is null)
        {
            return Result.Failure(VendorErrors.NotFound);
        }

        var unique = await VendorNameRule.EnsureUniqueAsync(
            repository, command.Details.Name, vendor.Id, cancellationToken);
        if (unique.IsFailure)
        {
            return unique;
        }

        var result = vendor.Update(command.Details, command.ActorId);
        if (result.IsFailure)
        {
            return result;
        }

        // A concurrent create/rename onto the same normalized name can pass the check above
        // too; the unique index rejects the loser here, and that is a 409 like the pre-check.
        if (!await repository.TrySaveChangesAsync(cancellationToken))
        {
            return Result.Failure(await VendorNameRule.ConflictAfterLostRaceAsync(
                repository, command.Details.Name, vendor.Id, cancellationToken));
        }

        return Result.Success();
    }
}
