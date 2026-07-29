namespace NorthernLink.Notifications.Domain.Dispatches;

/// <summary>Per-recipient outcome of one send action.</summary>
public enum DispatchRecipientStatus
{
    Sent,
    Failed,
}
