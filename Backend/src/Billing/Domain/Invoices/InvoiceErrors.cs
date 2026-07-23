using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices;

/// <summary>All domain errors the Invoice aggregate (and its handlers) can produce.</summary>
public static class InvoiceErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Billing.Invoice.NotFound", "The invoice was not found.");

    public static readonly Error NoActiveContract = Error.Validation(
        "Billing.Invoice.NoActiveContract", "No contract covers this client for the requested billing period.");

    public static readonly Error NotRoundTripBilled = Error.Validation(
        "Billing.Invoice.NotRoundTripBilled",
        "The client's contract is not billed per round trip; build this invoice with manual lines instead.");

    public static readonly Error InvalidPeriod = Error.Validation(
        "Billing.Invoice.InvalidPeriod", "The billing period end date cannot be before its start date.");

    public static readonly Error InvalidInvoiceNumber = Error.Validation(
        "Billing.Invoice.InvalidInvoiceNumber", "An invoice number is required.");

    public static readonly Error InvalidClientName = Error.Validation(
        "Billing.Invoice.InvalidClientName", "A client name is required.");

    public static readonly Error InvalidNetTerms = Error.Validation(
        "Billing.Invoice.InvalidNetTerms", "Net terms must be zero or more days.");

    public static readonly Error InvalidLineDescription = Error.Validation(
        "Billing.Invoice.InvalidLineDescription", "Every invoice line needs a description.");

    public static readonly Error InvalidLineQuantity = Error.Validation(
        "Billing.Invoice.InvalidLineQuantity", "An invoice line quantity must be greater than zero.");

    public static readonly Error InvalidLineUnitPrice = Error.Validation(
        "Billing.Invoice.InvalidLineUnitPrice", "An invoice line unit price cannot be negative.");

    public static readonly Error NotDraft = Error.Conflict(
        "Billing.Invoice.NotDraft", "Only a draft invoice can be edited or voided.");

    public static readonly Error NotSent = Error.Conflict(
        "Billing.Invoice.NotSent", "Only a sent invoice can be marked paid.");

    public static readonly Error AlreadySent = Error.Conflict(
        "Billing.Invoice.AlreadySent", "Only a draft invoice can be sent.");

    public static Error TripNotFound(Guid tripId) => Error.NotFound(
        "Billing.Invoice.TripNotFound", $"Billable trip {tripId} was not found for this tenant.");

    public static Error TripAlreadyInvoiced(Guid tripId) => Error.Conflict(
        "Billing.Invoice.TripAlreadyInvoiced", $"Billable trip {tripId} is already claimed by another invoice.");

    public static readonly Error InvalidQboSyncStatus = Error.Validation(
        "Billing.Invoice.InvalidQboSyncStatus", "Unknown QBO sync status.");

    public static readonly Error InvalidStatusFilter = Error.Validation(
        "Billing.Invoice.InvalidStatusFilter", "Unknown invoice status filter.");
}
