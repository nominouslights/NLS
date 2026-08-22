using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Rendering;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Application.Dispatches.SendTripPickupEmail;

/// <summary>
/// The single per-recipient composition used by both the send path and the preview: render the
/// template's subject and HTML body against each recipient's merge values, then derive the
/// plain-text fallback. Kept as one reusable helper so a preview can never drift from what an
/// actual send would produce — both call <see cref="Render"/> with the same inputs.
/// </summary>
public static class PickupEmailRenderer
{
    /// <summary>
    /// Renders one <see cref="OutgoingEmail"/> per recipient. Takes the individual trip fields
    /// (not the command) so it stays usable from both the send command and the preview query.
    /// </summary>
    public static IReadOnlyList<OutgoingEmail> Render(
        EmailTemplate template,
        string tripDate,
        string pickupTime,
        string dropoffTime,
        string route,
        string tripNumber,
        string? clientName,
        IReadOnlyList<RecipientInput> recipients) =>
        recipients
            .Select(recipient =>
            {
                var values = BuildValues(tripDate, pickupTime, dropoffTime, route, tripNumber, clientName, recipient);
                var subject = MergeFieldRenderer.RenderSubject(template.Subject, values);
                var htmlBody = MergeFieldRenderer.RenderHtml(template.HtmlBody, values);
                return new OutgoingEmail(recipient.Email.Trim(), subject, htmlBody, MergeFieldRenderer.RenderText(htmlBody));
            })
            .ToList();

    private static Dictionary<string, string> BuildValues(
        string tripDate,
        string pickupTime,
        string dropoffTime,
        string route,
        string tripNumber,
        string? clientName,
        RecipientInput recipient) =>
        new(StringComparer.Ordinal)
        {
            [MergeFields.PassengerName] = recipient.PassengerName,
            [MergeFields.TripDate] = tripDate,
            [MergeFields.PickupTime] = pickupTime,
            [MergeFields.DropoffTime] = dropoffTime,
            [MergeFields.Route] = route,
            [MergeFields.PickupStop] = recipient.PickupStop ?? string.Empty,
            [MergeFields.PickupAddress] = recipient.PickupAddress ?? string.Empty,
            [MergeFields.DropoffStop] = recipient.DropoffStop ?? string.Empty,
            [MergeFields.DropoffStopAddress] = recipient.DropoffStopAddress ?? string.Empty,
            [MergeFields.TripNumber] = tripNumber,
            [MergeFields.ClientName] = clientName ?? string.Empty,
        };
}
