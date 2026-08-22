using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Billing.Application.BillableTrips.GetBillableTrips;
using NorthernLink.Billing.Application.Invoices.GenerateDraft;
using NorthernLink.Billing.Application.Invoices.GetById;
using NorthernLink.Billing.Application.Invoices.GetInvoices;
using NorthernLink.Billing.Application.Invoices.ClearPayment;
using NorthernLink.Billing.Application.Invoices.ConfirmPayment;
using NorthernLink.Billing.Application.Invoices.MarkEntered;
using NorthernLink.Billing.Application.Invoices.ReplaceLines;
using NorthernLink.Billing.Application.Invoices.WriteOff;
using NorthernLink.Billing.Application.Invoices.UpdateQboReference;
using NorthernLink.Billing.Application.Invoices.Void;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Billing.Infrastructure.Endpoints;

/// <summary>
/// The Billing module's minimal-API surface under <c>/api/billing</c>. Every endpoint
/// resolves the ambient tenant (401 when absent — the API half of dual tenant
/// enforcement), stamps it onto the command/query, and dispatches via <see cref="ISender"/>.
/// </summary>
public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var invoices = app.MapGroup("/api/billing/invoices").RequireAuthorization();

        invoices.MapGet("", GetInvoices);
        invoices.MapGet("{id:guid}", GetInvoiceById);
        invoices.MapPost("generate-draft", GenerateDraft);
        invoices.MapPut("{id:guid}/lines", ReplaceLines);
        invoices.MapPost("{id:guid}/mark-entered", MarkEntered);
        invoices.MapPost("{id:guid}/qbo-reference", UpdateQboReference);
        invoices.MapPost("{id:guid}/confirm-payment", ConfirmPayment);
        invoices.MapPost("{id:guid}/clear-payment", ClearPayment);
        invoices.MapPost("{id:guid}/write-off", WriteOffInvoice);
        invoices.MapPost("{id:guid}/void", VoidInvoice);

        // The billable-trip pool the drafts draw from (uninvoiced view for manual lines).
        app.MapGet("/api/billing/billable-trips", GetBillableTrips).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> GetInvoices(
        string? status,
        Guid? clientId,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        // Matched against member names (case-insensitively, as before) rather than parsed:
        // Enum.TryParse also accepts "2"-style ordinals, which would silently rebind if the
        // enum ever gained a member.
        InvoiceStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var name = Enum.GetNames<InvoiceStatus>()
                .FirstOrDefault(n => n.Equals(status, StringComparison.OrdinalIgnoreCase));
            if (name is null)
            {
                return EndpointResults.Problem(InvoiceErrors.InvalidStatusFilter);
            }

            statusFilter = Enum.Parse<InvoiceStatus>(name);
        }

        var result = await sender.Query(new GetInvoicesQuery(tenantId, statusFilter, clientId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetInvoiceById(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetInvoiceByIdQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GenerateDraft(
        GenerateDraftInvoiceRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new GenerateDraftInvoiceCommand(tenantId, request.ClientId, request.PeriodStart, request.PeriodEnd),
            cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/billing/invoices/{result.Value}", new { id = result.Value })
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ReplaceLines(
        Guid id,
        ReplaceInvoiceLinesRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var lines = request.Lines
            .Select(line => new InvoiceLineInput(
                line.LineId,
                line.Description,
                line.TripIds,
                line.TripNumber,
                line.ServiceDate,
                line.Quantity,
                line.UnitPriceCad))
            .ToList();

        var result = await sender.Send(new ReplaceInvoiceLinesCommand(tenantId, id, lines), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> MarkEntered(
        Guid id,
        MarkEnteredRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new MarkInvoiceEnteredCommand(tenantId, id, request.QboInvoiceNumber, request.EnteredDate),
            cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ConfirmPayment(
        Guid id,
        ConfirmPaymentRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new ConfirmInvoicePaymentCommand(tenantId, id, request.ConfirmedDate),
            cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ClearPayment(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new ClearInvoicePaymentCommand(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Writes off an outstanding worksheet (EnteredInQbo → WrittenOff). The claimed trips stay
    /// claimed — they were billed once — and each moves to its own terminal WrittenOff.
    /// </summary>
    private static async Task<IResult> WriteOffInvoice(
        Guid id,
        WriteOffInvoiceRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new WriteOffInvoiceCommand(tenantId, id, request.AmountCad, request.WrittenOffDate, request.Reason),
            cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> VoidInvoice(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new VoidInvoiceCommand(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateQboReference(
        Guid id,
        QboReferenceRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new UpdateInvoiceQboReferenceCommand(tenantId, id, request.QboInvoiceNumber, request.EnteredDate),
            cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetBillableTrips(
        Guid? clientId,
        bool? uninvoiced,
        DateOnly? from,
        DateOnly? to,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(
            new GetBillableTripsQuery(tenantId, clientId, uninvoiced ?? false, from, to), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    // Request bodies — module-local contracts, mirrored by the frontend's typed client.

    public sealed record GenerateDraftInvoiceRequest(Guid ClientId, DateOnly PeriodStart, DateOnly PeriodEnd);

    public sealed record InvoiceLineRequest(
        Guid? LineId,
        string Description,
        IReadOnlyList<Guid>? TripIds,
        string? TripNumber,
        DateOnly? ServiceDate,
        decimal Quantity,
        decimal UnitPriceCad);

    public sealed record ReplaceInvoiceLinesRequest(IReadOnlyList<InvoiceLineRequest> Lines);

    public sealed record MarkEnteredRequest(string QboInvoiceNumber, DateOnly EnteredDate);

    public sealed record QboReferenceRequest(string QboInvoiceNumber, DateOnly EnteredDate);

    public sealed record ConfirmPaymentRequest(DateOnly ConfirmedDate);

    /// <summary>Body for POST /{id}/write-off. Reason is required, not decorative.</summary>
    public sealed record WriteOffInvoiceRequest(decimal AmountCad, DateOnly WrittenOffDate, string Reason);
}
