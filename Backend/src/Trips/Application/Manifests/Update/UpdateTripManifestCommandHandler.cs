using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Application.Manifests.Update;

public sealed class UpdateTripManifestCommandHandler(ITripManifestRepository repository)
    : ICommandHandler<UpdateTripManifestCommand>
{
    public async Task<Result> Handle(UpdateTripManifestCommand command, CancellationToken cancellationToken)
    {
        var manifest = await repository.GetByIdAsync(command.ManifestId, cancellationToken);
        if (manifest is null)
        {
            return Result.Failure(TripManifestErrors.NotFound);
        }

        var result = manifest.Update(
            command.TripDate,
            command.TripNumber,
            command.Route,
            command.Direction,
            command.Client,
            CarryExternalRefs(manifest.Passengers, command.Passengers),
            command.AllSeatbeltsVerified,
            command.Cargo,
            command.AllCargoSecured,
            command.Source,
            command.EnteredBy);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Keeps the Bookeo import's ownership of its rows across a manual edit from a client that
    /// does not round-trip <see cref="ManifestPassenger.ExternalRef"/>: an incoming row with no ref
    /// inherits the ref of an existing imported row with the same name, each ref claimed at most
    /// once. Without this a dispatcher fixing a seatbelt tick would silently turn every imported
    /// row into a "manual" one, and the next re-upload would add the same passengers again. A row
    /// that arrives WITH a ref keeps it as sent.
    /// </summary>
    public static IReadOnlyList<ManifestPassenger> CarryExternalRefs(
        IReadOnlyList<ManifestPassenger> existing,
        IReadOnlyList<ManifestPassenger> incoming)
    {
        var claimed = incoming
            .Where(p => p.ExternalRef is not null)
            .Select(p => p.ExternalRef!)
            .ToHashSet(StringComparer.Ordinal);

        var available = existing
            .Where(p => p.ExternalRef is not null && !claimed.Contains(p.ExternalRef))
            .ToList();

        if (available.Count == 0)
        {
            return incoming;
        }

        var result = new List<ManifestPassenger>(incoming.Count);
        foreach (var passenger in incoming)
        {
            if (passenger.ExternalRef is null)
            {
                var match = available.FirstOrDefault(p =>
                    string.Equals(p.Name.Trim(), passenger.Name?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    available.Remove(match);
                    result.Add(passenger with { ExternalRef = match.ExternalRef });
                    continue;
                }
            }

            result.Add(passenger);
        }

        return result;
    }
}
