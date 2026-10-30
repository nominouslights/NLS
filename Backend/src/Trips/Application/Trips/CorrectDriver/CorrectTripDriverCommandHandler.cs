using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.CorrectDriver;

/// <summary>
/// Applies a driver correction. Deliberately its own command, never routed through
/// <c>AssignTripCommandHandler</c>: assignment answers "who can drive this run?" and carries
/// availability gates (the driver must be Active, and Hours-of-Service fitness checks land on
/// that path), whereas a correction answers "who did drive it?" — a historical fact. So the
/// driver may be any row in driver_lookup for the tenant, Inactive or Deactivated included, and
/// no fitness, clearance, or billing check runs here. Nothing downstream stores the trip's driver
/// (Billing has none; Hours-of-Service is built from the driver's own duty events), so the
/// correction is local to the trip plus the published fact.
/// </summary>
public sealed class CorrectTripDriverCommandHandler(
    ITripRepository tripRepository,
    IDriverLookupRepository driverLookup,
    ICurrentActor actor)
    : ICommandHandler<CorrectTripDriverCommand>
{
    public async Task<Result> Handle(CorrectTripDriverCommand command, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure(TripErrors.NotFound);
        }

        // Documented order: status → reason → driver. The trip's own pre-checks run before the
        // lookup, so an unknown driver id (404) never masks a wrong-path or missing-reason refusal.
        var preCheck = trip.CanCorrectDriver(command.Reason);
        if (preCheck.IsFailure)
        {
            return preCheck;
        }

        // A missing/empty driver id is a malformed request (400 DriverRequired), not an unknown
        // driver (404): skip the lookup and let the aggregate refuse it with its own validation error.
        if (command.DriverId == Guid.Empty)
        {
            return trip.CorrectDriver(Guid.Empty, string.Empty, command.Reason, actor.UserId, actor.Email);
        }

        var driver = await driverLookup.GetAsync(command.DriverId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure(TripErrors.DriverNotFound);
        }

        var result = trip.CorrectDriver(
            driver.DriverId, driver.Name, command.Reason, actor.UserId, actor.Email);
        if (result.IsFailure)
        {
            return result;
        }

        await tripRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
