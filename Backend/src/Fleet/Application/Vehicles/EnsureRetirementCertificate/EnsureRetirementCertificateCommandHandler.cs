using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Application.Vehicles.EnsureRetirementCertificate;

/// <summary>
/// Idempotent ensure-exists for a retired vehicle's certificate. Delivery is at-least-once,
/// so this must never double-issue: it no-ops when the vehicle isn't visible/retired or a
/// certificate already exists (check-before-insert on vehicle id). Runs under the ambient
/// tenant the projection worker set from the journal row.
/// </summary>
public sealed class EnsureRetirementCertificateCommandHandler(IVehicleRepository repository)
    : ICommandHandler<EnsureRetirementCertificateCommand>
{
    public async Task<Result> Handle(EnsureRetirementCertificateCommand command, CancellationToken cancellationToken)
    {
        var vehicle = await repository.GetByIdAsync(command.VehicleId, cancellationToken);
        if (vehicle is null || vehicle.Status != VehicleStatus.Retired)
        {
            // Not this tenant's row, or not (yet) retired — nothing to ensure.
            return Result.Success();
        }

        if (await repository.HasCertificateAsync(command.VehicleId, cancellationToken))
        {
            // Already issued (the inline path is the common case) — idempotent no-op.
            return Result.Success();
        }

        await RetirementCertificateIssuer.IssueAsync(vehicle, repository, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
