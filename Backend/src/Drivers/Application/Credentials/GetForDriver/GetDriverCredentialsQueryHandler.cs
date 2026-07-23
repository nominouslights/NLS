using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Credentials.GetForDriver;

public sealed class GetDriverCredentialsQueryHandler(IDriverCredentialReadService readService)
    : IQueryHandler<GetDriverCredentialsQuery, IReadOnlyList<DriverCredentialResponse>>
{
    public async Task<Result<IReadOnlyList<DriverCredentialResponse>>> Handle(
        GetDriverCredentialsQuery query,
        CancellationToken cancellationToken)
    {
        var credentials = await readService.GetForDriverAsync(query.DriverId, cancellationToken);
        return Result.Success(credentials);
    }
}
