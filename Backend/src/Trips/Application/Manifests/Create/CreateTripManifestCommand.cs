using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Application.Manifests.Create;

/// <summary>
/// Records a passenger + cargo manifest for a trip. Entered from the Driver Field App
/// (App source) or the Dispatch Console (Dispatcher source — must say who entered it).
/// <paramref name="TenantId"/> is stamped by the endpoint layer from the ambient tenant
/// context. Returns the new manifest's id.
/// </summary>
public sealed record CreateTripManifestCommand(
    Guid TenantId,
    DateOnly TripDate,
    string TripNumber,
    string Route,
    TripDirection? Direction,
    string? Client,
    IReadOnlyList<ManifestPassenger> Passengers,
    bool AllSeatbeltsVerified,
    IReadOnlyList<ManifestCargoItem> Cargo,
    CargoSecuredStatus? AllCargoSecured,
    ManifestSource Source,
    string? EnteredBy) : ICommand<Guid>;
