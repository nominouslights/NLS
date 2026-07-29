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
}
