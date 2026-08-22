namespace NorthernLink.Notifications.Application.Dispatches;

/// <summary>
/// The data for one pickup-email report PDF — a flat, presentation-ready snapshot of a single
/// trip's pickup send: the trip context in the header, then one <see cref="PickupEmailReportItem"/>
/// per recipient. All fields are already-formatted strings composed by the handler; the PDF
/// renderer does no domain lookups of its own.
/// </summary>
public sealed record PickupEmailReport(
    string TripNumber,
    string Route,
    string TripDate,
    string TemplateName,
    DateTimeOffset SentAtUtc,
    int SentCount,
    int FailedCount,
    IReadOnlyList<PickupEmailReportItem> Items);

/// <summary>
/// One recipient's line in the report: who it went to, whether it was sent, and a copy of the
/// exact subject and (plain-text) body that recipient received.
/// </summary>
public sealed record PickupEmailReportItem(
    string PassengerName,
    string Email,
    string Status,
    string Subject,
    string BodyText);
