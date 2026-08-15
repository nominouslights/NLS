using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.CreateDeadheadReturn;

/// <summary>
/// Mints the new leg's trip number from the same per-tenant sequence as ad-hoc creation,
/// then delegates eligibility and the reversed-leg shape to
/// <see cref="Trip.CreateDeadheadReturn"/>. The return leg's driver/vehicle default to the
/// source trip's own (snapshots included); an explicit override on the command goes
/// through the same lookup validation as POST /assign. A refused source burns a number,
/// which is acceptable — trip numbers are unique, not gapless (see
/// <see cref="ITripNumberGenerator"/>). One save commits the new leg and the source's
/// pairing atomically.
/// </summary>
public sealed class CreateDeadheadReturnCommandHandler(
    ITripRepository tripRepository,
    IDriverLookupRepository driverLookup,
    IVehicleLookupRepository vehicleLookup,
    ITripNumberGenerator tripNumberGenerator)
    : ICommandHandler<CreateDeadheadReturnCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateDeadheadReturnCommand command, CancellationToken cancellationToken)
    {
        var source = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (source is null)
        {
            return Result.Failure<Guid>(TripErrors.NotFound);
        }

        // Driver for the return leg: explicit override validated against driver_lookup,
        // otherwise inherited from the source (whose own creation already validated it).
        Guid driverId;
        string driverName;
        if (command.DriverId is { } overrideDriverId)
        {
            var driver = await driverLookup.GetAsync(overrideDriverId, cancellationToken);
            if (driver is null)
            {
                return Result.Failure<Guid>(TripErrors.DriverNotFound);
            }

            if (!driver.IsActive)
            {
                return Result.Failure<Guid>(TripErrors.DriverNotActive);
            }

            driverId = driver.DriverId;
            driverName = driver.Name;
        }
        else if (source.DriverId is { } sourceDriverId && source.DriverName is { } sourceDriverName)
        {
            driverId = sourceDriverId;
            driverName = sourceDriverName;
        }
        else
        {
            return Result.Failure<Guid>(TripErrors.DriverRequired);
        }

        // Vehicle for the return leg — the unit driven back. Same override-or-inherit rule.
        Guid vehicleId;
        string vehicleUnit;
        if (command.VehicleId is { } overrideVehicleId)
        {
            var vehicle = await vehicleLookup.GetAsync(overrideVehicleId, cancellationToken);
            if (vehicle is null)
            {
                return Result.Failure<Guid>(TripErrors.VehicleNotFound);
            }

            if (!vehicle.IsActive)
            {
                return Result.Failure<Guid>(TripErrors.VehicleNotActive);
            }

            vehicleId = vehicle.VehicleId;
            vehicleUnit = vehicle.UnitNumber;
        }
        else if (source.VehicleId is { } sourceVehicleId && source.VehicleUnit is { } sourceVehicleUnit)
        {
            vehicleId = sourceVehicleId;
            vehicleUnit = sourceVehicleUnit;
        }
        else
        {
            return Result.Failure<Guid>(TripErrors.VehicleRequired);
        }

        var tripNumber = await tripNumberGenerator.NextAsync(source.TenantId, cancellationToken);

        var returnTrip = source.CreateDeadheadReturn(tripNumber, driverId, driverName, vehicleId, vehicleUnit);
        if (returnTrip.IsFailure)
        {
            return Result.Failure<Guid>(returnTrip.Error);
        }

        tripRepository.Add(returnTrip.Value);
        await tripRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(returnTrip.Value.Id);
    }
}
