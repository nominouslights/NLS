using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Application.Manifests.Update;

/// <summary>
/// Revises an existing manifest's §1 trip info, passengers, and cargo. Loaded by
/// <paramref name="ManifestId"/> under the ambient tenant (404 when not visible), mutated,
/// and saved — the mutation raises the journaled update event stamped with
/// <paramref name="Source"/> + <paramref name="EnteredBy"/> (the audit entry). A
/// Dispatcher-sourced edit must record who made it.
/// </summary>
public sealed record UpdateTripManifestCommand(
    Guid ManifestId,
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
    string? EnteredBy) : ICommand;
