using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Vehicles.EnsureRetirementCertificate;

/// <summary>
/// Same-module secondary command dispatched by the projection worker when a
/// <c>VehicleReachedEndOfLifeDomainEvent</c> appears in the journal: idempotently ensures the
/// auto-retired vehicle has a retirement certificate. It normally finds one already — the
/// change-status / record-odometer handlers issue the certificate inline in the same
/// transaction as the auto-retire — so this is an at-least-once safety net, guarded by a
/// check-before-insert (see the handler). The worker runs it under the journal row's tenant.
/// </summary>
public sealed record EnsureRetirementCertificateCommand(Guid VehicleId) : ICommand;
