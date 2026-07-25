using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections;

/// <summary>
/// Fleet's reaction to a completed trip manifest: materialize one pre-trip and one
/// post-trip <see cref="VehicleInspection"/> from the event's checklists — the
/// "manifest auto-creates the fleet inspection entries" rule, satisfied without a
/// library reference. Runs outside any HTTP request, so the tenant comes from the
/// event payload, not ITenantContext. Delivery is at-least-once: idempotent via a
/// check-before-insert on (manifestId, type), backstopped by the table's unique index.
/// Post-trip items are pass/fail only — an unconfirmed item becomes a Minor defect.
/// </summary>
public sealed class TripManifestCompletedIntegrationEventHandler(
    IVehicleInspectionRepository repository,
    ILogger<TripManifestCompletedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TripManifestCompletedIntegrationEvent>
{
    public const string UnconfirmedPostTripNote = "Not confirmed on trip manifest";

    public async Task Handle(TripManifestCompletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var source = integrationEvent.Source == "Paper"
            ? InspectionSource.PaperTranscription
            : InspectionSource.DriverApp;

        var added = 0;

        if (!await repository.ExistsForManifestAsync(
                integrationEvent.TenantId, integrationEvent.ManifestId, InspectionType.PreTrip, cancellationToken))
        {
            repository.Add(CreatePreTripInspection(integrationEvent, source));
            added++;
        }

        if (!await repository.ExistsForManifestAsync(
                integrationEvent.TenantId, integrationEvent.ManifestId, InspectionType.PostTrip, cancellationToken))
        {
            repository.Add(CreatePostTripInspection(integrationEvent, source));
            added++;
        }

        if (added > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Fleet materialized {Added} inspection(s) from manifest {ManifestId} ({EventId}): trip {TripNumber}, unit {Unit}",
            added,
            integrationEvent.ManifestId,
            integrationEvent.EventId,
            integrationEvent.TripNumber,
            integrationEvent.Unit);
    }

    private static VehicleInspection CreatePreTripInspection(
        TripManifestCompletedIntegrationEvent integrationEvent,
        InspectionSource source)
    {
        var checklist = integrationEvent.PreTripItems
            .Select(item => new InspectionChecklistItem
            {
                Group = item.Group,
                Item = item.Item,
                Passed = item.Status != "Fail",
            })
            .ToList();

        var defects = integrationEvent.PreTripItems
            .Where(item => item.Status == "Fail")
            .Select(item => new InspectionDefect
            {
                Item = item.Item,
                Severity = ParseSeverity(item.Severity),
                Note = item.Note,
            })
            .ToList();

        return VehicleInspection.Create(
            integrationEvent.TenantId,
            integrationEvent.Unit,
            InspectionType.PreTrip,
            integrationEvent.DriverName,
            source,
            integrationEvent.EnteredBy,
            integrationEvent.TripNumber,
            integrationEvent.ManifestId,
            integrationEvent.PerformedAt,
            integrationEvent.OdometerStartKm,
            checklist,
            defects);
    }

    private static VehicleInspection CreatePostTripInspection(
        TripManifestCompletedIntegrationEvent integrationEvent,
        InspectionSource source)
    {
        var checklist = integrationEvent.PostTripItems
            .Select(item => new InspectionChecklistItem { Item = item.Item, Passed = item.Ok })
            .ToList();

        var defects = integrationEvent.PostTripItems
            .Where(item => !item.Ok)
            .Select(item => new InspectionDefect
            {
                Item = item.Item,
                Severity = InspectionDefectSeverity.Minor,
                Note = UnconfirmedPostTripNote,
            })
            .ToList();

        return VehicleInspection.Create(
            integrationEvent.TenantId,
            integrationEvent.Unit,
            InspectionType.PostTrip,
            integrationEvent.DriverName,
            source,
            integrationEvent.EnteredBy,
            integrationEvent.TripNumber,
            integrationEvent.ManifestId,
            integrationEvent.PerformedAt,
            integrationEvent.OdometerEndKm,
            checklist,
            defects);
    }

    /// <summary>Severity strings come from the event (public contract); unknowns degrade to Minor.</summary>
    private static InspectionDefectSeverity ParseSeverity(string? severity) =>
        Enum.TryParse<InspectionDefectSeverity>(severity, out var parsed)
            ? parsed
            : InspectionDefectSeverity.Minor;
}
