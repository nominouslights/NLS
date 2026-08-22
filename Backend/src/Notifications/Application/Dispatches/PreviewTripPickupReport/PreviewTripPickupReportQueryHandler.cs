using System.Text.RegularExpressions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Dispatches.SendTripPickupEmail;
using NorthernLink.Notifications.Domain.Dispatches;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Application.Dispatches.PreviewTripPickupReport;

/// <summary>
/// Handles <see cref="PreviewTripPickupReportQuery"/>: loads and gates the template exactly like
/// the send (NotFound / Inactive / ServiceTypeMismatch / ClientMismatch) → validates recipients
/// (1–16, RFC-lite email) like the send → renders the pickup emails and composes the report via
/// the shared <see cref="PickupEmailRenderer"/> / <see cref="PickupEmailReportComposer"/>. Every
/// report item is marked "Preview" because nothing is sent. Reads only — no dispatch is recorded
/// and <see cref="IEmailSender"/> is never involved.
/// </summary>
public sealed partial class PreviewTripPickupReportQueryHandler(
    IEmailTemplateRepository templateRepository,
    IPickupEmailReportPdf reportPdf)
    : IQueryHandler<PreviewTripPickupReportQuery, PickupReportPreviewResponse>
{
    public async Task<Result<PickupReportPreviewResponse>> Handle(
        PreviewTripPickupReportQuery query,
        CancellationToken cancellationToken)
    {
        var template = await templateRepository.GetByIdAsync(query.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure<PickupReportPreviewResponse>(EmailTemplateErrors.NotFound);
        }

        if (!template.IsActive)
        {
            return Result.Failure<PickupReportPreviewResponse>(EmailTemplateErrors.Inactive);
        }

        if (template.ServiceType != query.ServiceType)
        {
            return Result.Failure<PickupReportPreviewResponse>(EmailTemplateErrors.ServiceTypeMismatch);
        }

        // A service-wide template (null ClientId) is valid for any trip of its service type;
        // a client-pinned one only for that exact client (a client-less trip never matches).
        if (template.ClientId is not null && template.ClientId != query.ClientId)
        {
            return Result.Failure<PickupReportPreviewResponse>(EmailTemplateErrors.ClientMismatch);
        }

        if (query.Recipients.Count == 0)
        {
            return Result.Failure<PickupReportPreviewResponse>(EmailDispatchErrors.NoRecipients);
        }

        if (query.Recipients.Count > EmailDispatch.MaxRecipients)
        {
            return Result.Failure<PickupReportPreviewResponse>(EmailDispatchErrors.TooManyRecipients);
        }

        foreach (var recipient in query.Recipients)
        {
            if (!EmailRegex().IsMatch(recipient.Email?.Trim() ?? string.Empty))
            {
                return Result.Failure<PickupReportPreviewResponse>(
                    EmailDispatchErrors.InvalidRecipientEmail(recipient.Email ?? string.Empty));
            }
        }

        var outgoing = PickupEmailRenderer.Render(
            template,
            query.TripDate,
            query.PickupTime,
            query.DropoffTime,
            query.Route,
            query.TripNumber,
            query.ClientName,
            query.Recipients);

        // Every item is "Preview": nothing has been sent, so there is no Sent/Failed outcome.
        var items = query.Recipients
            .Select((recipient, index) => new PickupEmailReportItem(
                recipient.PassengerName.Trim(),
                recipient.Email.Trim(),
                "Preview",
                outgoing[index].Subject,
                outgoing[index].TextBody))
            .ToList();

        var composition = PickupEmailReportComposer.Compose(
            new PickupReportContext(
                query.TripNumber,
                query.Route,
                query.TripDate,
                template.Name,
                DateTimeOffset.UtcNow,
                SentCount: 0,
                FailedCount: 0,
                items),
            reportPdf);

        var reportRecipients = query.ReportRecipients
            .Select(address => address?.Trim() ?? string.Empty)
            .Where(address => EmailRegex().IsMatch(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var response = new PickupReportPreviewResponse(
            composition.Subject,
            composition.HtmlBody,
            composition.TextBody,
            Convert.ToBase64String(composition.PdfBytes),
            query.Recipients.Count,
            reportRecipients);

        return Result.Success(response);
    }

    // RFC-lite, identical to the send path's gate: something@something.tld, no whitespace.
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();
}
