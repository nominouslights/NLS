using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Application.Vendors;

/// <summary>
/// The case-insensitive name-uniqueness check both create and update run. Compares
/// <see cref="Vendor.NormalizedName"/> keys, so "acme fuel" collides with "ACME Fuel"; a vendor
/// never collides with itself, so renaming "Acme fuel" to "Acme Fuel" is allowed. The unique
/// (tenant_id, normalized_name) index is the race backstop; this is what makes every other
/// collision a readable 409 naming the vendor that already holds the name.
/// </summary>
public static class VendorNameRule
{
    public static async Task<Result> EnsureUniqueAsync(
        IVendorRepository repository, string? name, Guid? selfId, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByNormalizedNameAsync(Vendor.NormalizeName(name), cancellationToken);

        return existing is null || existing.Id == selfId
            ? Result.Success()
            : Result.Failure(VendorErrors.DuplicateName(existing.Name, existing.IsActive));
    }

    /// <summary>
    /// The 409 for a save that lost the race on the unique index
    /// (<see cref="IVendorRepository.TrySaveChangesAsync"/> returned false). Re-reads the winner —
    /// one index seek on a cleared unit of work — so the message names it exactly as the pre-check
    /// would have; if the winner is no longer visible (renamed or deleted since), falls back to
    /// <see cref="VendorErrors.DuplicateNameTaken"/>. Same code either way.
    /// </summary>
    public static async Task<Error> ConflictAfterLostRaceAsync(
        IVendorRepository repository, string? name, Guid? selfId, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByNormalizedNameAsync(Vendor.NormalizeName(name), cancellationToken);

        return existing is not null && existing.Id != selfId
            ? VendorErrors.DuplicateName(existing.Name, existing.IsActive)
            : VendorErrors.DuplicateNameTaken;
    }
}
