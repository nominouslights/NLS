using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Application.Vehicles.GetRetirementCertificate;

public sealed class GetRetirementCertificateQueryHandler(IVehicleReadService readService)
    : IQueryHandler<GetRetirementCertificateQuery, RetirementCertificateResponse>
{
    public async Task<Result<RetirementCertificateResponse>> Handle(
        GetRetirementCertificateQuery query,
        CancellationToken cancellationToken)
    {
        var certificate = await readService.GetRetirementCertificateAsync(query.VehicleId, cancellationToken);

        return certificate is null
            ? Result.Failure<RetirementCertificateResponse>(VehicleErrors.CertificateNotFound)
            : Result.Success(certificate);
    }
}
