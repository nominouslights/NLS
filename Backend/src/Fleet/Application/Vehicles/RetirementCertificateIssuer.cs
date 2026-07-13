using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Application.Vehicles;

/// <summary>
/// Shared by the change-status and record-odometer handlers: every transition into
/// Retired — manual or automatic at end of life — issues a certificate in the same
/// unit of work as the status change.
/// </summary>
internal static class RetirementCertificateIssuer
{
    public static async Task IssueAsync(
        Vehicle vehicle,
        IVehicleRepository repository,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var sequence = await repository.NextCertificateSequenceAsync(vehicle.TenantId, now.Year, cancellationToken);
        repository.AddCertificate(RetirementCertificate.Issue(vehicle, sequence, now));
    }
}
