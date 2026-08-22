using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Notifications.Application.Dispatches.GetTripEmailHistory;
using NorthernLink.Notifications.Application.Dispatches.SendTripPickupEmail;
using NorthernLink.Notifications.Application.Templates.Activate;
using NorthernLink.Notifications.Application.Templates.Create;
using NorthernLink.Notifications.Application.Templates.Deactivate;
using NorthernLink.Notifications.Application.Templates.GetTemplateById;
using NorthernLink.Notifications.Application.Templates.GetTemplates;
using NorthernLink.Notifications.Application.Templates.Preview;
using NorthernLink.Notifications.Application.Templates.Update;
using NorthernLink.Notifications.Domain;

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

        // Emails — send + per-trip history.
        notifications.MapPost("emails/trip-pickup", SendTripPickupEmail);
        notifications.MapGet("emails", GetTripEmailHistory);

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
                    r.PickupStop, r.PickupAddress, r.DropoffStop, r.DropoffStopAddress))
                .ToList());

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetTripEmailHistory(
        Guid tripId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetTripEmailHistoryQuery(tenantId, tripId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }
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
/// Recipients: 1–16.
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
    List<RecipientRequest>? Recipients);

/// <summary>One selected manifest passenger: address plus per-passenger merge values.</summary>
public sealed record RecipientRequest(
    string? Email,
    string? PassengerName,
    string? PickupStop,
    string? PickupAddress,
    string? DropoffStop,
    string? DropoffStopAddress);
