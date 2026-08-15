using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Shipments.BulkAssign;

/// <summary>
/// Routes several shipments onto one trip in a single action — batching a Friday run means
/// picking nine parcels at once, and nine round trips through a modal is the difference between
/// the feature being used and abandoned.
/// </summary>
public sealed record BulkAssignShipmentsCommand(
    Guid TripId,
    IReadOnlyList<Guid> ShipmentIds) : ICommand<BulkAssignResult>;

/// <summary>
/// Deliberately not atomic: one already-picked-up parcel must not block the other eight. Each
/// failure is reported with the error the domain gave, so the UI can say which and why.
/// </summary>
public sealed record BulkAssignResult(int Assigned, IReadOnlyList<BulkAssignFailure> Failures);

public sealed record BulkAssignFailure(Guid ShipmentId, string Code, string Message);
