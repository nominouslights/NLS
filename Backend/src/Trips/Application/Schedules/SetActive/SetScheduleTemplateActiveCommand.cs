using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Schedules.SetActive;

/// <summary>
/// Activates or deactivates a template. Deactivation stops future generation only —
/// already-materialized trips are untouched (cancel them individually if needed).
/// </summary>
public sealed record SetScheduleTemplateActiveCommand(Guid TemplateId, bool Active) : ICommand;
