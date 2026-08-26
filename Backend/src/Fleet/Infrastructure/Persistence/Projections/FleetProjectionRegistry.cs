using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Fleet.Application.Inspections.PropagateOdometer;
using NorthernLink.Fleet.Application.Maintenance.Completions.PropagateOdometer;
using NorthernLink.Fleet.Application.Vehicles.EnsureRetirementCertificate;
using NorthernLink.Fleet.Domain.Inspections.Events;
using NorthernLink.Fleet.Domain.Maintenance.Events;
using NorthernLink.Fleet.Domain.Vehicles.Events;

namespace NorthernLink.Fleet.Infrastructure.Persistence.Projections;

/// <summary>
/// The single source of truth for Fleet's read-side registry: every projection and every
/// same-module <c>OnEvent</c> reaction, in one place. Called by
/// <see cref="FleetServiceCollectionExtensions.AddFleet"/> (via <c>AddProjections</c>) and by
/// the integration-test fixture, so the tests can never silently exercise a narrower read side
/// — or fewer reactions — than the API composes.
///
/// Retirement certificates are driven by vehicle events (they're created inline during a
/// vehicle's retirement, so they share the "vehicle" aggregate's journal) — hence two
/// projections keyed on the same aggregate type. A newly entered inspection advances the linked
/// vehicle's odometer intra-Fleet (monotonic, auto-retire applies) — the same same-module
/// reaction pattern.
/// </summary>
public static class FleetProjectionRegistry
{
    public static void Configure(ProjectionRegistryBuilder<FleetDbContext> registry) => registry
        .Project(new VehicleProjection())
        .Project(new RetirementCertificateProjection())
        .Project(new ShopProjection())
        .Project(new VehicleDocumentProjection())
        .Project(new ServiceRecordProjection())
        .Project(new WorkOrderProjection())
        .Project(new VehicleInspectionProjection())
        .Project(new MaintenancePlanProjection())
        .Project(new PlanAssignmentProjection())
        .Project(new PmCompletionProjection())
        .OnEvent<VehicleReachedEndOfLifeDomainEvent>(entry =>
            new EnsureRetirementCertificateCommand(entry.AggregateId))
        .OnEvent<VehicleInspectionCreatedDomainEvent>(entry =>
            new PropagateInspectionOdometerCommand(entry.TenantId, entry.AggregateId))
        // A corrected odometer still flows to the vehicle. The vehicle odometer is monotonic
        // (Vehicle.RecordOdometer no-ops a non-advancing reading), so a downward correction
        // will not roll the vehicle back — acceptable: the inspection remains the record of
        // what was actually read.
        .OnEvent<VehicleInspectionAmendedDomainEvent>(entry =>
            new PropagateInspectionOdometerCommand(entry.TenantId, entry.AggregateId))
        // A PM completion's odometer reading advances the vehicle exactly as inspection
        // readings do (monotonic — a historical entry below the current reading no-ops),
        // so a fresh completion cannot leave the master odometer stale and deflate every
        // other line's due math.
        .OnEvent<PmCompletionLoggedDomainEvent>(entry =>
            new PropagatePmOdometerCommand(entry.TenantId, entry.AggregateId));
}
