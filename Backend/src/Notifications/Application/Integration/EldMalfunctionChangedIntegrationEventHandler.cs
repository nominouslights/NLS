using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Notifications.Application.Integration;

/// <summary>
/// Emails the tenant's compliance officers (<see cref="Roles.ComplianceAccess"/> — Owner and
/// ComplianceOfficer, never dispatch) when an ELD malfunction is reported, and again when it is
/// resolved. The event carries the driver's name and every fact the email states, so nothing is
/// looked up outside this module; the audience comes from the <c>user_lookup</c> replica.
/// Any other status is logged and ignored rather than parked — a future status must not stall
/// the consumer. Replay-safe through <see cref="ComplianceAlertMailer"/>'s deterministic ids.
/// </summary>
public sealed class EldMalfunctionChangedIntegrationEventHandler(
    ComplianceAlertMailer mailer,
    ILogger<EldMalfunctionChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<EldMalfunctionChangedIntegrationEvent>
{
    public const string OpenKind = "eld-malfunction-open";
    public const string ResolvedKind = "eld-malfunction-resolved";

    public const string OpenTemplateName = "Built-in: ELD malfunction reported";
    public const string ResolvedTemplateName = "Built-in: ELD malfunction resolved";

    /// <summary>s.78 (M.R. 72/2007): a driver whose ELD fails keeps paper logs until it is repaired.</summary>
    public const string PaperLogsLine =
        "Paper logs are expected until resolved (s.78); resolve it in the Drivers & Compliance app.";

    public async Task Handle(EldMalfunctionChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            switch (integrationEvent.Status)
            {
                case "Open":
                    await mailer.SendAsync(Opened(integrationEvent), cancellationToken);
                    break;
                case "Resolved":
                    await mailer.SendAsync(Resolved(integrationEvent), cancellationToken);
                    break;
                default:
                    logger.LogWarning(
                        "ELD malfunction {ReportId} arrived with unknown status {Status}; no alert sent ({EventId})",
                        integrationEvent.ReportId, integrationEvent.Status, integrationEvent.EventId);
                    break;
            }
        }
    }

    public static string OpenSubject(EldMalfunctionChangedIntegrationEvent e) =>
        $"ELD malfunction reported — {e.DriverName} ({e.Code})";

    public static string ResolvedSubject(EldMalfunctionChangedIntegrationEvent e) =>
        $"ELD malfunction resolved — {e.DriverName} ({e.Code})";

    public static ComplianceAlert Opened(EldMalfunctionChangedIntegrationEvent e) => new(
        e.TenantId,
        e.EventId,
        OpenKind,
        Roles.ComplianceAccess,
        OpenTemplateName,
        OpenSubject(e),
        $"""
        <p>An ELD malfunction has been reported and the driver is on paper logs.</p>
        <table>
          <tr><th align="left">Driver</th><td>{ComplianceAlertMailer.Html(e.DriverName)}</td></tr>
          <tr><th align="left">Malfunction code</th><td>{ComplianceAlertMailer.Html(e.Code)}</td></tr>
          <tr><th align="left">Noticed</th><td>{ComplianceAlertMailer.LocalTime(e.NoticedAtUtc)}</td></tr>
          <tr><th align="left">Description</th><td>{ComplianceAlertMailer.Html(e.Description)}</td></tr>
        </table>
        <p><strong>{ComplianceAlertMailer.Html(PaperLogsLine)}</strong></p>
        <p>— Northern Link Shuttle &amp; Cargo</p>
        """);

    public static ComplianceAlert Resolved(EldMalfunctionChangedIntegrationEvent e) => new(
        e.TenantId,
        e.EventId,
        ResolvedKind,
        Roles.ComplianceAccess,
        ResolvedTemplateName,
        ResolvedSubject(e),
        $"""
        <p>The ELD malfunction <strong>{ComplianceAlertMailer.Html(e.Code)}</strong> reported for
        <strong>{ComplianceAlertMailer.Html(e.DriverName)}</strong> (noticed {ComplianceAlertMailer.LocalTime(e.NoticedAtUtc)})
        has been resolved. The driver is back on the ELD; make sure the paper-log days were transcribed.</p>
        <p>— Northern Link Shuttle &amp; Cargo</p>
        """);
}
