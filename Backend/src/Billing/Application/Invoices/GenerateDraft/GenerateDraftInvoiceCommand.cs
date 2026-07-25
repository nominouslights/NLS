using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.GenerateDraft;

/// <summary>Generates a draft invoice for one client's billing period from its completed,
/// uninvoiced round trips at the contract rate. Returns the new invoice id.</summary>
public sealed record GenerateDraftInvoiceCommand(
    Guid TenantId,
    Guid ClientId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd) : ICommand<Guid>;
