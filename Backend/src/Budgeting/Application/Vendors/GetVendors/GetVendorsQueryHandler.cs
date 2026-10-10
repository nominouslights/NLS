using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Application.Vendors.GetVendors;

/// <summary>Handles <see cref="GetVendorsQuery"/>. An empty register is an empty list, not an error.</summary>
public sealed class GetVendorsQueryHandler(IVendorReadService readService)
    : IQueryHandler<GetVendorsQuery, IReadOnlyList<VendorResponse>>
{
    public async Task<Result<IReadOnlyList<VendorResponse>>> Handle(
        GetVendorsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await readService.GetVendorsAsync(query.IncludeInactive, cancellationToken));
}
