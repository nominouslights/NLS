using NorthernLink.Notifications.Application.Dispatches;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Tests;

/// <summary>Shared builders for Notifications tests.</summary>
internal static class TestNotifications
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>A small but fully populated accruals report — every section has a row.</summary>
    public static ClientAccrualsReport SampleAccrualsReport(
        string clientName = "Vale Manitoba Operations",
        string periodLabel = "August 2026") => new(
        clientName,
        periodLabel,
        "August 29, 2026",
        Notes: ["Estimates use the contract rate of $1,450.00 per round trip."],
        Summary:
        [
            new AccrualsSummaryRow("Paid", "2", "$2,900.00", "—"),
            new AccrualsSummaryRow("Ready for billing", "1", "—", "$1,450.00 est."),
        ],
        Buckets:
        [
            new AccrualsReportBucket(
                "Paid",
                [
                    new AccrualsGroupRow(
                        "Aug 4", "NL-1042 / NL-1043", "Thompson – Lynn Lake",
                        "PO-88231", "INV-2026-081", "$1,450.00"),
                ]),
            new AccrualsReportBucket(
                "Ready for billing",
                [
                    new AccrualsGroupRow(
                        "Aug 18", "NL-1077 / NL-1078", "Thompson – Lynn Lake",
                        "PO-88231", "—", "$1,450.00 est."),
                ]),
        ],
        Reconciliation:
        [
            new AccrualsReconciliationRow(
                "Aug 11", "NL-1055", "Thompson – Lynn Lake", "Cancelled", "Weather", "—"),
        ],
        Invoices:
        [
            new AccrualsInvoiceRow(
                "INV-2026-081", "QBO-4417", "Paid", "Aug 1 – Aug 31, 2026", "$2,900.00"),
        ]);

    /// <summary>A two-traveller community booking pass sheet, every field populated.</summary>
    public static BookingPassSheet SampleBookingPassSheet(
        string reference = "NL-7K3M2Q",
        string customerName = "Doris Spence",
        string? notes = "Wheelchair-accessible seating requested.",
        IReadOnlyList<BookingPassTraveller>? travellers = null) => new(
        reference,
        "Tuesday, September 15, 2026",
        "Thompson ↔ Lynn Lake",
        "Thompson Depot",
        "Lynn Lake Terminal",
        customerName,
        "E-Transfer",
        "Unpaid",
        notes,
        travellers ??
        [
            new BookingPassTraveller("Doris Spence", "204-555-0199", "1 of 2"),
            new BookingPassTraveller("Sam Spence", null, "2 of 2"),
        ]);

    public static EmailTemplate CreateTemplate(
        string name = "Community pickup",
        NotificationServiceType serviceType = NotificationServiceType.Community,
        Guid? clientId = null,
        string? clientName = null,
        string subject = "Pickup {{TripDate}} — {{TripNumber}}",
        string htmlBody = "<p>Hi {{PassengerName}},</p><p>Pickup at {{PickupStop}} at {{PickupTime}}.</p><p>Arrival at {{DropoffTime}}.</p>")
    {
        var result = EmailTemplate.Create(TenantId, name, serviceType, clientId, clientName, subject, htmlBody);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test template invalid: {result.Error.Code}");
        }

        return result.Value;
    }
}
