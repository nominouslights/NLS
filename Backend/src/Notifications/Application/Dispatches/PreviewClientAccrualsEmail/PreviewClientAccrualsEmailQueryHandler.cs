using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Dispatches.SendClientAccrualsEmail;
using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Application.Dispatches.PreviewClientAccrualsEmail;

/// <summary>
/// Handles <see cref="PreviewClientAccrualsEmailQuery"/>: gates the client snapshot and the
/// recipients exactly like the send (shared <see cref="AccrualsRecipientGate"/>) → composes
/// the covering email + report PDF via the shared <see cref="ClientAccrualsEmailComposer"/>.
/// Reads only — no dispatch is recorded, and <see cref="IEmailSender"/> is deliberately not
/// injected, so this handler is structurally unable to send.
/// </summary>
public sealed class PreviewClientAccrualsEmailQueryHandler(IClientAccrualsReportPdf reportPdf)
    : IQueryHandler<PreviewClientAccrualsEmailQuery, AccrualsEmailPreviewResponse>
{
    public Task<Result<AccrualsEmailPreviewResponse>> Handle(
        PreviewClientAccrualsEmailQuery query,
        CancellationToken cancellationToken)
    {
        // Same client snapshot gate as the send, so a doomed send fails at preview time too.
        if (query.ClientId == Guid.Empty || string.IsNullOrWhiteSpace(query.ClientName))
        {
            return Task.FromResult(
                Result.Failure<AccrualsEmailPreviewResponse>(EmailDispatchErrors.ClientRequired));
        }

        var recipientsResult = AccrualsRecipientGate.Normalize(query.Recipients);
        if (recipientsResult.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<AccrualsEmailPreviewResponse>(recipientsResult.Error));
        }

        var recipients = recipientsResult.Value;
        var composition = ClientAccrualsEmailComposer.Compose(query.Report, reportPdf);

        var response = new AccrualsEmailPreviewResponse(
            composition.Subject,
            composition.HtmlBody,
            composition.TextBody,
            Convert.ToBase64String(composition.PdfBytes),
            recipients.Count,
            recipients.Select(recipient => recipient.Email).ToList());

        return Task.FromResult(Result.Success(response));
    }
}
