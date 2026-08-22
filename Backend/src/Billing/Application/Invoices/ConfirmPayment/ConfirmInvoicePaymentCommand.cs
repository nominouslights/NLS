using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.ConfirmPayment;

/// <summary>
/// Records that payment against an entered worksheet's QBO invoice has been confirmed —
/// entered by hand, no QBO API calls anywhere.
/// </summary>
public sealed record ConfirmInvoicePaymentCommand(
    Guid TenantId,
    Guid InvoiceId,
    DateOnly ConfirmedDate) : ICommand;
