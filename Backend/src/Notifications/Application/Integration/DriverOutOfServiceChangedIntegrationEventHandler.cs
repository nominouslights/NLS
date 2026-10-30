using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Notifications.Application.Integration;

/// <summary>
/// Emails compliance <i>and</i> dispatch (<see cref="Roles.ComplianceAccess"/> plus
/// <see cref="Roles.DispatchAccess"/>, de-duplicated — Owner sits in both) when a driver is
/// placed out of service: dispatch has to stop assigning the driver right now, which is why
/// this alert is wider than the ELD one. A lift is deliberately not emailed — the
/// hours gate and the roster show it, and a "you may assign them again" email invites a
/// reassignment before anyone has checked the record. The event carries no driver name
/// (<c>DriverOutOfService</c> holds none), so the email names the driver by id and points at
/// the app; see the HOS plan's follow-ups.
/// </summary>
public sealed class DriverOutOfServiceChangedIntegrationEventHandler(
    ComplianceAlertMailer mailer,
    ILogger<DriverOutOfServiceChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<DriverOutOfServiceChangedIntegrationEvent>
{
    public const string ActiveKind = "out-of-service-active";
    public const string ActiveTemplateName = "Built-in: driver placed out of service";
    public const string UntilLifted = "until lifted";

    /// <summary>Longest slice of the reason that goes into the subject line; the body carries it whole.</summary>
    public const int SubjectReasonMaxLength = 120;

    /// <summary>Compliance plus dispatch, each role once.</summary>
    public static readonly string[] Audience =
        [.. Roles.ComplianceAccess.Union(Roles.DispatchAccess, StringComparer.Ordinal)];

    public async Task Handle(DriverOutOfServiceChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            if (integrationEvent.Status != "Active")
            {
                logger.LogInformation(
                    "Out-of-service record {RecordId} changed to {Status}; no alert for that status ({EventId})",
                    integrationEvent.RecordId, integrationEvent.Status, integrationEvent.EventId);
                return;
            }

            await mailer.SendAsync(PlacedOutOfService(integrationEvent), cancellationToken);
        }
    }

    /// <summary>"until {Winnipeg time}" or "until lifted" — the span's end as the subject states it.</summary>
    public static string UntilText(DateTimeOffset? untilUtc) =>
        untilUtc is { } until ? $"until {ComplianceAlertMailer.LocalTime(until)}" : UntilLifted;

    public static string ActiveSubject(DriverOutOfServiceChangedIntegrationEvent e)
    {
        var reason = e.Reason.Trim();
        if (reason.Length > SubjectReasonMaxLength)
        {
            reason = reason[..SubjectReasonMaxLength].TrimEnd() + "…";
        }

        return $"Driver placed out of service — {UntilText(e.UntilUtc)}: {reason}";
    }

    public static ComplianceAlert PlacedOutOfService(DriverOutOfServiceChangedIntegrationEvent e) => new(
        e.TenantId,
        e.EventId,
        ActiveKind,
        Audience,
        ActiveTemplateName,
        ActiveSubject(e),
        $"""
        <p>A driver has been placed out of service. <strong>Do not assign, claim or start a trip for
        this driver while the declaration stands</strong> — the hours gate refuses it, and the roster
        shows the driver as out of service.</p>
        <table>
          <tr><th align="left">Driver</th><td>driver record {e.DriverId:D}</td></tr>
          <tr><th align="left">Declared</th><td>{ComplianceAlertMailer.LocalTime(e.DeclaredAtUtc)}</td></tr>
          <tr><th align="left">In force</th><td>{ComplianceAlertMailer.Html(UntilText(e.UntilUtc))}</td></tr>
          <tr><th align="left">Reason</th><td>{ComplianceAlertMailer.Html(e.Reason)}</td></tr>
        </table>
        <p>The full record, and the lift, are in the Drivers &amp; Compliance app.</p>
        <p>— Northern Link Shuttle &amp; Cargo</p>
        """);
}
