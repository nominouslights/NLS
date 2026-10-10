using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Vendors;

namespace NorthernLink.Budgeting.Application.Vendors.GetVendorById;

/// <summary>Handles <see cref="GetVendorByIdQuery"/>. A missing (or other tenant's) vendor is NotFound.</summary>
public sealed class GetVendorByIdQueryHandler(IVendorReadService readService)
    : IQueryHandler<GetVendorByIdQuery, VendorResponse>
{
    public async Task<Result<VendorResponse>> Handle(GetVendorByIdQuery query, CancellationToken cancellationToken)
    {
        var vendor = await readService.GetVendorAsync(query.VendorId, cancellationToken);

        return vendor is null
            ? Result.Failure<VendorResponse>(VendorErrors.NotFound)
            : Result.Success(vendor);
    }
}
