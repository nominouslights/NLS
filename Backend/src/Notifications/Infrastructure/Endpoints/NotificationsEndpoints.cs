using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Notifications.Application.Dispatches;
using NorthernLink.Notifications.Application.Dispatches.GetClientEmailHistory;
using NorthernLink.Notifications.Application.Dispatches.GetTripEmailHistory;
using NorthernLink.Notifications.Application.Dispatches.PreviewClientAccrualsEmail;
using NorthernLink.Notifications.Application.Dispatches.PreviewTripPickupReport;
using NorthernLink.Notifications.Application.Dispatches.SendClientAccrualsEmail;
using NorthernLink.Notifications.Application.Dispatches.SendTripPickupEmail;
using NorthernLink.Notifications.Application.Templates.Activate;
using NorthernLink.Notifications.Application.Templates.Create;
using NorthernLink.Notifications.Application.Templates.Deactivate;
using NorthernLink.Notifications.Application.Templates.GetTemplateById;
using NorthernLink.Notifications.Application.Templates.GetTemplates;
using NorthernLink.Notifications.Application.Templates.Preview;
using NorthernLink.Notifications.Application.Templates.Update;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Infrastructure.Endpoints;

/// <summary>
/// The Notifications module's minimal-API surface under <c>/api/notifications</c>. Every
/// endpoint resolves the ambient tenant (401 when absent — the API half of dual tenant
/// enforcement), stamps it onto the command/query, and dispatches via <see cref="ISender"/>.
/// The send endpoint returns 200 with per-recipient outcomes even on partial/total provider
/// failure — outcomes are data the dispatcher must see, not an error path.
/// </summary>
public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        // DispatchAccess (Owner, Dispatcher, Supervisor): authoring templates and emailing
        // passengers is dispatch work — registered gateway-side in AuthorizationPolicyRegistration.
        var notifications = app.MapGroup("/api/notifications")
            .RequireAuthorization(AuthorizationPolicies.DispatchAccess);

        // Templates.
        notifications.MapGet("templates", GetTemplates);
        notifications.MapPost("templates", CreateTemplate);
        notifications.MapPost("templates/preview", PreviewTemplate);
        notifications.MapGet("templates/{id:guid}", GetTemplateById);
        notifications.MapPut("templates/{id:guid}", UpdateTemplate);
        notifications.MapPost("templates/{id:guid}/activate", ActivateTemplate);
        notifications.MapPost("templates/{id:guid}/deactivate", DeactivateTemplate);

        // Emails — sends + previews + history (filtered by trip or by client).
        notifications.MapPost("emails/trip-pickup", SendTripPickupEmail);
        notifications.MapPost("emails/trip-pickup/report-preview", PreviewTripPickupReport);
        notifications.MapPost("emails/client-accruals", SendClientAccrualsEmail);
        notifications.MapPost("emails/client-accruals/preview", PreviewClientAccrualsEmail);
        notifications.MapGet("emails", GetEmailHistory);

        return app;
    }

    private static async Task<IResult> GetTemplates(
        NotificationServiceType? serviceType,
        Guid? clientId,
        bool? includeInactive,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(
            new GetEmailTemplatesQuery(tenantId, serviceType, clientId, includeInactive ?? false),
            cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetTemplateById(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetEmailTemplateByIdQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateTemplate(
        EmailTemplateRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateEmailTemplateCommand(
            tenantId,
            request.Name ?? string.Empty,
            request.ServiceType,
            request.ClientId,
            request.ClientName,
            request.Subject ?? string.Empty,
            request.HtmlBody ?? string.Empty);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/notifications/templates/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateTemplate(
        Guid id, EmailTemplateRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateEmailTemplateCommand(
            tenantId,
            id,
            request.Name ?? string.Empty,
            request.ServiceType,
            request.ClientId,
            request.ClientName,
            request.Subject ?? string.Empty,
            request.HtmlBody ?? string.Empty);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ActivateTemplate(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new ActivateEmailTemplateCommand(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> DeactivateTemplate(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new DeactivateEmailTemplateCommand(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> PreviewTemplate(
        PreviewEmailTemplateRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var query = new PreviewEmailTemplateQuery(
            tenantId,
            request.Subject ?? string.Empty,
            request.HtmlBody ?? string.Empty,
            request.Values);

        var result = await sender.Query(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> SendTripPickupEmail(
        SendTripPickupEmailRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new SendTripPickupEmailCommand(
            tenantId,
            request.DispatchId,
            request.TemplateId,
            request.TripId,
            request.TripNumber ?? string.Empty,
            request.ManifestId,
            request.ServiceType,
            request.TripDate ?? string.Empty,
            request.PickupTime ?? string.Empty,
            request.DropoffTime ?? string.Empty,
            request.Route ?? string.Empty,
            request.ClientId,
            request.ClientName,
            (request.Recipients ?? [])
                .Select(r => new RecipientInput(
                    r.Email ?? string.Empty, r.PassengerName ?? string.Empty,
                    r.PickupStop, r.PickupAddress, r.DropoffStop, r.DropoffStopAddress,
                    r.PickupTime, r.DropoffTime))
                .ToList(),
            request.ReportRecipients ?? []);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> PreviewTripPickupReport(
        PreviewTripPickupReportRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var query = new PreviewTripPickupReportQuery(
            tenantId,
            request.TemplateId,
            request.TripId,
            request.TripNumber ?? string.Empty,
            request.ManifestId,
            request.ServiceType,
            request.TripDate ?? string.Empty,
            request.PickupTime ?? string.Empty,
            request.DropoffTime ?? string.Empty,
            request.Route ?? string.Empty,
            request.ClientId,
            request.ClientName,
            (request.Recipients ?? [])
                .Select(r => new RecipientInput(
                    r.Email ?? string.Empty, r.PassengerName ?? string.Empty,
                    r.PickupStop, r.PickupAddress, r.DropoffStop, r.DropoffStopAddress,
                    r.PickupTime, r.DropoffTime))
                .ToList(),
            request.ReportRecipients ?? []);

        var result = await sender.Query(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> SendClientAccrualsEmail(
        SendClientAccrualsEmailRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new SendClientAccrualsEmailCommand(
            tenantId,
            request.DispatchId,
            request.ClientId,
            request.ClientName ?? string.Empty,
            request.ServiceType,
            ToReport(request.Report),
            ToRecipients(request.Recipients));

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> PreviewClientAccrualsEmail(
        PreviewClientAccrualsEmailRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var query = new PreviewClientAccrualsEmailQuery(
            tenantId,
            request.ClientId,
            request.ClientName ?? string.Empty,
            request.ServiceType,
            ToReport(request.Report),
            ToRecipients(request.Recipients));

        var result = await sender.Query(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetEmailHistory(
        Guid? tripId, Guid? clientId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        // One filter is required; tripId wins when both are supplied (the trip view is the
        // narrower, older contract).
        if (tripId is { } trip)
        {
            var result = await sender.Query(new GetTripEmailHistoryQuery(tenantId, trip), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
        }

        if (clientId is { } client)
        {
            var result = await sender.Query(new GetClientEmailHistoryQuery(tenantId, client), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
        }

        return EndpointResults.Problem(EmailDispatchErrors.HistoryFilterRequired);
    }

    /// <summary>Normalizes the nullable request tree into the Application's non-null report record.</summary>
    private static ClientAccrualsReport ToReport(ClientAccrualsReportRequest? report) => new(
        report?.ClientName ?? string.Empty,
        report?.PeriodLabel ?? string.Empty,
        report?.PreparedDate ?? string.Empty,
        (report?.Notes ?? []).Select(note => note ?? string.Empty).ToList(),
        (report?.Summary ?? [])
            .Select(row => new AccrualsSummaryRow(
                row.BucketLabel ?? string.Empty,
                row.RoundTrips ?? string.Empty,
                row.ActualCad ?? string.Empty,
                row.EstimatedCad ?? string.Empty))
            .ToList(),
        (report?.Buckets ?? [])
            .Select(bucket => new AccrualsReportBucket(
                bucket.Label ?? string.Empty,
                (bucket.Rows ?? [])
                    .Select(row => new AccrualsGroupRow(
                        row.Date ?? string.Empty,
                        row.TripNumbers ?? string.Empty,
                        row.Route ?? string.Empty,
                        row.PoNumber ?? string.Empty,
                        row.Reference ?? string.Empty,
                        row.AmountCad ?? string.Empty))
                    .ToList()))
            .ToList(),
        (report?.Reconciliation ?? [])
            .Select(row => new AccrualsReconciliationRow(
                row.Date ?? string.Empty,
                row.TripNumbers ?? string.Empty,
                row.Route ?? string.Empty,
                row.Status ?? string.Empty,
                row.Reason ?? string.Empty,
                row.AmountCad ?? string.Empty))
            .ToList(),
        (report?.Invoices ?? [])
            .Select(invoice => new AccrualsInvoiceRow(
                invoice.InvoiceNumber ?? string.Empty,
                invoice.Status ?? string.Empty,
                invoice.SubtotalCad ?? string.Empty,
                invoice.GstCad ?? string.Empty,
                invoice.TotalCad ?? string.Empty))
            .ToList());

    private static List<AccrualsRecipientInput> ToRecipients(List<AccrualsRecipientRequest>? recipients) =>
        (recipients ?? [])
            .Select(r => new AccrualsRecipientInput(r.Email ?? string.Empty, r.ContactName ?? string.Empty))
            .ToList();
}

/// <summary>Body of a successful create (201, with Location header).</summary>
public sealed record EntityCreatedResponse(Guid Id);

/// <summary>
/// Request body for POST/PUT /api/notifications/templates. ServiceType is the enum name,
/// e.g. "Community". ClientId + ClientName (a display snapshot) are set together for a
/// client-specific template, or both omitted for a service-type-wide one.
/// </summary>
public sealed record EmailTemplateRequest(
    string? Name,
    NotificationServiceType ServiceType,
    Guid? ClientId,
    string? ClientName,
    string? Subject,
    string? HtmlBody);

/// <summary>
/// Request body for POST /api/notifications/templates/preview. Renders arbitrary (possibly
/// unsaved) content; null Values renders with server sample data.
/// </summary>
public sealed record PreviewEmailTemplateRequest(
    string? Subject,
    string? HtmlBody,
    Dictionary<string, string>? Values);

/// <summary>
/// Request body for POST /api/notifications/emails/trip-pickup. DispatchId is a
/// client-generated GUID — the idempotency key; replaying it returns the stored dispatch
/// without re-sending. Trip fields are opaque snapshots composed by the dispatcher's screen;
/// ClientId (null = client-less trip) is validated against the template's client pin.
/// Recipients: 1–16. ReportRecipients are the pre-resolved contact email addresses (supplied
/// by the frontend) that receive the best-effort report email with a PDF of each sent pickup
/// email attached — only acted on for ContractCrew trips; omit or empty to skip the report.
/// </summary>
public sealed record SendTripPickupEmailRequest(
    Guid DispatchId,
    Guid TemplateId,
    Guid TripId,
    string? TripNumber,
    Guid? ManifestId,
    NotificationServiceType ServiceType,
    string? TripDate,
    string? PickupTime,
    string? DropoffTime,
    string? Route,
    Guid? ClientId,
    string? ClientName,
    List<RecipientRequest>? Recipients,
    IReadOnlyList<string>? ReportRecipients);

/// <summary>
/// Request body for POST /api/notifications/emails/trip-pickup/report-preview. Mirrors
/// <see cref="SendTripPickupEmailRequest"/> minus <c>DispatchId</c> — a preview records nothing,
/// so there is no idempotency key. Renders the pickup emails and composes the report (summary +
/// PDF) with the exact send-time composition and returns it without sending anything.
/// ReportRecipients are echoed back (valid + distinct) for display only.
/// </summary>
public sealed record PreviewTripPickupReportRequest(
    Guid TemplateId,
    Guid TripId,
    string? TripNumber,
    Guid? ManifestId,
    NotificationServiceType ServiceType,
    string? TripDate,
    string? PickupTime,
    string? DropoffTime,
    string? Route,
    Guid? ClientId,
    string? ClientName,
    List<RecipientRequest>? Recipients,
    IReadOnlyList<string>? ReportRecipients);

/// <summary>
/// One selected manifest passenger: address plus per-passenger merge values.
/// <paramref name="PickupTime"/>/<paramref name="DropoffTime"/> are optional — supply them to
/// give this passenger the time the vehicle reaches their own stop; omit them to fall back to
/// the request's trip-level times.
/// </summary>
public sealed record RecipientRequest(
    string? Email,
    string? PassengerName,
    string? PickupStop,
    string? PickupAddress,
    string? DropoffStop,
    string? DropoffStopAddress,
    string? PickupTime,
    string? DropoffTime);

/// <summary>
/// Request body for POST /api/notifications/emails/client-accruals. DispatchId is a
/// client-generated GUID — the idempotency key; replaying it returns the stored dispatch
/// without re-sending. ClientId + ClientName (a display snapshot) anchor the recorded
/// dispatch; ServiceType is the client's service category as the enum name. Report is the
/// fully composed, pre-formatted accruals report (the frontend derives it — Notifications
/// holds no trips/billing data); Recipients are the pre-resolved contact addresses (1–16
/// after de-duplication).
/// </summary>
public sealed record SendClientAccrualsEmailRequest(
    Guid DispatchId,
    Guid ClientId,
    string? ClientName,
    NotificationServiceType ServiceType,
    ClientAccrualsReportRequest? Report,
    List<AccrualsRecipientRequest>? Recipients);

/// <summary>
/// Request body for POST /api/notifications/emails/client-accruals/preview. Mirrors
/// <see cref="SendClientAccrualsEmailRequest"/> minus <c>DispatchId</c> — a preview records
/// nothing, so there is no idempotency key. Composes the covering email and report PDF with
/// the exact send-time composition and returns them without sending anything.
/// </summary>
public sealed record PreviewClientAccrualsEmailRequest(
    Guid ClientId,
    string? ClientName,
    NotificationServiceType ServiceType,
    ClientAccrualsReportRequest? Report,
    List<AccrualsRecipientRequest>? Recipients);

/// <summary>One selected client contact: address plus the display name recorded in history.</summary>
public sealed record AccrualsRecipientRequest(string? Email, string? ContactName);

/// <summary>
/// The flat accruals report as posted by the frontend — pre-formatted strings only (labels,
/// dates, amounts with any "est." markings baked in); the backend renders it verbatim.
/// </summary>
public sealed record ClientAccrualsReportRequest(
    string? ClientName,
    string? PeriodLabel,
    string? PreparedDate,
    List<string?>? Notes,
    List<AccrualsSummaryRowRequest>? Summary,
    List<AccrualsReportBucketRequest>? Buckets,
    List<AccrualsReconciliationRowRequest>? Reconciliation,
    List<AccrualsInvoiceRowRequest>? Invoices);

/// <summary>One bucket's line in the summary table.</summary>
public sealed record AccrualsSummaryRowRequest(
    string? BucketLabel,
    string? RoundTrips,
    string? ActualCad,
    string? EstimatedCad);

/// <summary>One billing-state bucket's detail section.</summary>
public sealed record AccrualsReportBucketRequest(
    string? Label,
    List<AccrualsGroupRowRequest>? Rows);

/// <summary>One round-trip group's line in a bucket table.</summary>
public sealed record AccrualsGroupRowRequest(
    string? Date,
    string? TripNumbers,
    string? Route,
    string? PoNumber,
    string? Reference,
    string? AmountCad);

/// <summary>One cancelled/written-off group's line in the reconciliation section.</summary>
public sealed record AccrualsReconciliationRowRequest(
    string? Date,
    string? TripNumbers,
    string? Route,
    string? Status,
    string? Reason,
    string? AmountCad);

/// <summary>One referenced invoice's line — the only place GST appears in the report.</summary>
public sealed record AccrualsInvoiceRowRequest(
    string? InvoiceNumber,
    string? Status,
    string? SubtotalCad,
    string? GstCad,
    string? TotalCad);
