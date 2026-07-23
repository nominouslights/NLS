namespace NorthernLink.Clients.Domain.Contracts;

/// <summary>
/// Contract lifecycle. Contracts start <see cref="Active"/>; <see cref="Ended"/> marks a
/// contract whose period ran out naturally, <see cref="Terminated"/> one cut short by
/// <c>Terminate()</c>. Only <see cref="Active"/> contracts participate in the one-active-
/// contract-per-client overlap rule.
/// </summary>
public enum ContractStatus
{
    Active,
    Ended,
    Terminated,
}
