using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Application.Manifests.Create;

public sealed class CreateTripManifestCommandHandler(ITripManifestRepository repository)
    : ICommandHandler<CreateTripManifestCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTripManifestCommand command, CancellationToken cancellationToken)
    {
        var manifestResult = TripManifest.Create(
            command.TenantId,
            command.TripDate,
            command.TripNumber,
            command.Route,
            command.Direction,
            command.Client,
            command.Unit,
            command.DriverName,
            command.DriverLicenceNo,
            command.LicencePlate,
            command.OdometerStartKm,
            command.FuelLevel,
            command.PreTrip,
            command.Weather,
            command.TemperatureC,
            command.RoadConditions,
            command.Visibility,
            command.RoadAdvisories,
            command.Passengers,
            command.AllSeatbeltsVerified,
            command.Cargo,
            command.AllCargoSecured,
            command.Issues,
            command.NoIssues,
            command.DepartureTime,
            command.ArrivalTime,
            command.OdometerEndKm,
            command.TotalKm,
            command.FuelAdded,
            command.FuelLitres,
            command.FuelCostCad,
            command.PostTrip,
            command.Attestations,
            command.DriverSignatureName,
            command.CertifiedAt ?? DateTimeOffset.UtcNow,
            command.Source,
            command.EnteredBy);

        if (manifestResult.IsFailure)
        {
            return Result.Failure<Guid>(manifestResult.Error);
        }

        repository.Add(manifestResult.Value);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(manifestResult.Value.Id);
    }
}
