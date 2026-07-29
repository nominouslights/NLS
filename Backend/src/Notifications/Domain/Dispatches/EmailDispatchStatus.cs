namespace NorthernLink.Notifications.Domain.Dispatches;

/// <summary>
/// Overall outcome of one send action, derived from the per-recipient outcomes:
/// every recipient sent → <see cref="Sent"/>; some sent → <see cref="PartiallyFailed"/>;
/// none sent → <see cref="Failed"/>.
/// </summary>
public enum EmailDispatchStatus
{
    Sent,
    PartiallyFailed,
    Failed,
}
