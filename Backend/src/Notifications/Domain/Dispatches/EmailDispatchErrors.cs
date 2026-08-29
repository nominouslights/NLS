using NorthernLink.Shared.Kernel;

namespace NorthernLink.Notifications.Domain.Dispatches;

/// <summary>All domain errors the EmailDispatch aggregate (and its handlers) can produce.</summary>
public static class EmailDispatchErrors
{
    public static readonly Error NoRecipients = Error.Validation(
        "Notifications.Dispatch.NoRecipients", "At least one recipient is required.");

    public static readonly Error TooManyRecipients = Error.Validation(
        "Notifications.Dispatch.TooManyRecipients",
        $"A dispatch is limited to {EmailDispatch.MaxRecipients} recipients.");

    /// <summary>A recipient whose contact value does not look like an email address.</summary>
    public static Error InvalidRecipientEmail(string email) => Error.Validation(
        "Notifications.Dispatch.InvalidRecipientEmail",
        $"\"{email}\" is not a valid email address.");

    /// <summary>A client accruals dispatch without its anchor — the client id + name snapshot.</summary>
    public static readonly Error ClientRequired = Error.Validation(
        "Notifications.Dispatch.ClientRequired",
        "A client accruals dispatch requires the client's id and name.");

    /// <summary>The email-history endpoint was called with neither a tripId nor a clientId filter.</summary>
    public static readonly Error HistoryFilterRequired = Error.Validation(
        "Notifications.Dispatch.HistoryFilterRequired",
        "Provide a tripId or clientId query parameter.");
}
