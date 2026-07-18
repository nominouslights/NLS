using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.Enter;

/// <summary>
/// Dispatcher paper-backup DVIR entry (the office fallback for a failed Driver App).
/// Returns the new inspection's id.
/// </summary>
public sealed record EnterInspectionCommand(
    Guid TenantId,
    string Unit,
    InspectionType Type,
    string DriverName,
    string? EnteredBy,
    DateTimeOffset PerformedAt,
    int? OdometerKm,
    IReadOnlyList<ChecklistItemInput> Checklist,
    IReadOnlyList<DefectInput> Defects) : ICommand<Guid>;

/// <summary>One checklist row on a dispatcher inspection request.</summary>
public sealed record ChecklistItemInput(string? Group, string Item, bool Passed);

/// <summary>One defect on a dispatcher inspection request.</summary>
public sealed record DefectInput(string Item, InspectionDefectSeverity Severity, string? Note);
